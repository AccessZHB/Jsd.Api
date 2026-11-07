using Jsd.Api.Entities;
using Jsd.Api.Exceptions;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;
using Jsd.Api.Models.Price;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Jsd.Api.Services;

/// <summary>
/// 订单服务实现。
///
/// ┌──────────────────────────────────────────────────────────────────────┐
/// │ 一、状态流转（本系统【无审核环节】，支付即生效）                                                                                           │
/// │   0 待付款 Pending ──支付──▶ 1 已支付 Paid ──发货──▶ 2 已发货 Shipped                                                              │
/// │                                                        │                                                                                  │
/// │                                                    确认收货                                                                                │
/// │                                                        ▼                                                                                  │
/// │                                                  3 已完成 Completed                                                                        │
/// │   0 待付款 Pending ──关闭──▶ 4 已关闭 Closed                                                                                           │
/// └──────────────────────────────────────────────────────────────────────┘
///
/// 二、库存 / 销量联动（本模块与库存模块的唯一耦合点）
///   ● 状态 → 已支付(Paid)：按明细逐条【扣减 prod_sku.stock】，并写 log_stock_log
///     （change_type=4 订单扣减，ref_type='order'，ref_id=订单ID）。
///     采用【乐观锁 CAS】扣减，防止并发超卖；扣减后库存为负直接抛业务异常回滚。
///   ● 下单创建（SubmitAsync / CreateAsync）：同步按商品聚合累加【prod_info.sales_count】
///     （驱动商品列表"已售"）。与订单写入同事务，订单失败回滚则不累加；同一订单仅创建一次，
///     天然幂等，不会因重复调用接口 / 重复支付回调而重复累加。
///   ● 状态 → 已完成(Completed)：按明细逐条【增加 prod_sku.sales_count】（销量）。
///     注意：完成阶段【不再动库存】——库存已在支付时扣过，避免重复扣减。
///   ● 其余状态变更（发货/关闭/改备注）不涉及库存与销量。
///
/// 三、事务
///   创建订单、支付、完成 均涉及多表写入，统一用 IDbContextTransaction 包裹，
///   任一环节失败整体回滚（库存、日志、订单状态要么全成功要么全失败）。
///
/// 四、金额口径（后端重算，不信任前端传值）
///   total_amount = Σ(明细 unit_price × quantity)
///   pay_amount   = total_amount - discount_amount + freight_amount（小于 0 时归零）
/// </summary>
public class OrderService : IOrderService
{
    /// <summary>订单号前缀（ORD = Order）</summary>
    private const string NoPrefix = "ORD";

    /// <summary>变动类型：订单扣减（对应 log_stock_log.change_type=4）</summary>
    private const int ChangeTypeOrder = 4;

    /// <summary>关联类型：订单（log_stock_log.ref_type='order'）</summary>
    private const string RefTypeOrder = "order";

    /// <summary>乐观锁 CAS 最大重试次数</summary>
    private const int MaxCasRetry = 3;

    /// <summary>
    /// 变动日志操作人。
    /// 说明：本模块暂未接入当前登录用户上下文，自动库存变动统一记为 system；
    /// 后续接入 CurrentUserService 后，把此处替换为当前登录用户名即可。
    /// </summary>
    private const string SystemOperator = "system";

    private readonly AppDbContext _db;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _itemRepository;
    private readonly IRepository<MemMember> _memberRepository;
    private readonly IReceivableService _receivableService;
    private readonly IPriceService _priceService;
    private readonly IBalanceService _balanceService;
    private readonly CurrentUserService _currentUser;

    public OrderService(
        AppDbContext db,
        IOrderRepository orderRepository,
        IOrderItemRepository itemRepository,
        IRepository<MemMember> memberRepository,
        IReceivableService receivableService,
        IPriceService priceService,
        IBalanceService balanceService,
        CurrentUserService currentUser)
    {
        _db = db;
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _memberRepository = memberRepository;
        _receivableService = receivableService;
        _priceService = priceService;
        _balanceService = balanceService;
        _currentUser = currentUser;
    }

    // ============================================================
    // 1. 分页查询订单列表
    // ============================================================

    /// <summary>
    /// 分页查询订单（订单号 / 状态 / 创建时间范围 筛选），返回主表信息 + 商品总数。
    /// 商品总数用一次 group by 统计当前页所有订单，避免逐单查询产生 N+1。
    /// </summary>
    public async Task<ApiResponse<PagedResult<OrderListDto>>> GetPagedListAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime,
        long? buyerId, string? buyerKeyword, string? receiverKeyword,
        int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, total) = await _orderRepository.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, buyerId, buyerKeyword, receiverKeyword, page, pageSize);

        var list = orders.Select(MapToListDto).ToList();

        // 批量回填买家名称，避免前端显示"买家#ID"（一次查询，不做 N+1）
        var buyerIds = list.Select(o => o.BuyerId).Distinct().ToList();
        if (buyerIds.Count > 0)
        {
            var buyerNames = await _db.MemMembers.AsNoTracking()
                .Where(m => buyerIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            foreach (var dto in list)
            {
                dto.BuyerName = buyerNames.GetValueOrDefault(dto.BuyerId);
            }
        }

        // 一次性统计当前页每张订单的明细数（商品种类数）与商品件数合计
        var orderIds = list.Select(o => o.Id).ToList();
        if (orderIds.Count > 0)
        {
            var summaries = await _db.TrxOrderItems
                .Where(i => orderIds.Contains(i.OrderId))
                .GroupBy(i => i.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    ItemCount = g.Count(),
                    TotalQuantity = g.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(x => x.OrderId, x => x);

            foreach (var dto in list)
            {
                if (summaries.TryGetValue(dto.Id, out var s))
                {
                    dto.ItemCount = s.ItemCount;
                    dto.TotalQuantity = s.TotalQuantity;
                }
            }
        }

        return ApiResponse<PagedResult<OrderListDto>>.Success(
            PagedResult<OrderListDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 1.5 会员「我的订单」（强制归属当前登录会员，绝不越权）
    // ============================================================

    /// <summary>
    /// 会员「我的订单」：仅返回当前登录会员（JWT member_id → BuyerId）的订单。
    /// 1) 会员ID 一律从 Token 解析（_currentUser.MemberId），前端不传、也不信任；
    /// 2) EF 查询强制 .Where(o => o.BuyerId == 当前会员ID)；
    /// 3) 管理员令牌（无 member_id 声明）不应调用本接口，MemberId 回退到 UserId 时直接拒绝。
    /// </summary>
    public async Task<ApiResponse<PagedResult<OrderListDto>>> GetMyOrdersAsync(
        int? orderStatus, DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        // ⚠️ 会员身份必须从 Token 取，绝不信任前端；非会员令牌直接拒绝
        var memberId = _currentUser.MemberId;
        if (memberId <= 0 || !_currentUser.IsMember)
        {
            return ApiResponse<PagedResult<OrderListDto>>.Fail("无法识别当前会员身份", 401);
        }

        var query = _db.TrxOrders
            .AsNoTracking()
            .Where(o => o.IsDeleted == 0 && o.BuyerId == memberId);   // 强制归属过滤

        // 订单状态筛选
        if (orderStatus.HasValue)
        {
            query = query.Where(o => o.OrderStatus == orderStatus.Value);
        }

        // 创建时间范围（按天查询时 endTime 传当天 23:59:59）
        if (startTime.HasValue)
        {
            query = query.Where(o => o.CreateTime >= startTime.Value);
        }
        if (endTime.HasValue)
        {
            var end = endTime.Value.TimeOfDay == TimeSpan.Zero
                ? endTime.Value.AddDays(1).AddSeconds(-1)
                : endTime.Value;
            query = query.Where(o => o.CreateTime <= end);
        }

        var total = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = orders.Select(MapToListDto).ToList();

        // 一次性统计当前页每张订单的明细数（商品种类数）与商品件数合计（与 GetPagedListAsync 一致）
        var orderIds = list.Select(o => o.Id).ToList();
        if (orderIds.Count > 0)
        {
            var summaries = await _db.TrxOrderItems
                .Where(i => orderIds.Contains(i.OrderId))
                .GroupBy(i => i.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    ItemCount = g.Count(),
                    TotalQuantity = g.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(x => x.OrderId, x => x);

            foreach (var dto in list)
            {
                if (summaries.TryGetValue(dto.Id, out var s))
                {
                    dto.ItemCount = s.ItemCount;
                    dto.TotalQuantity = s.TotalQuantity;
                }
            }
        }

        return ApiResponse<PagedResult<OrderListDto>>.Success(
            PagedResult<OrderListDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 会员「我的订单」各状态数量统计：
    /// 1) 会员身份从 Token 解析（_currentUser.MemberId），非会员令牌直接拒绝；
    /// 2) 强制 .Where(o => o.IsDeleted == 0 &amp;&amp; o.BuyerId == 当前会员ID)（归属过滤）；
    /// 3) 按 OrderStatus 分组计数，映射到 { pendingPay, pendingShip, pendingReceive, completed, closed }，
    ///    不存在的状态补 0（如当前会员没有任何「待付款」订单，pendingPay 返回 0 而非 null）。
    /// 一次分组查询替代原前端分 5 次请求 /order/my，减少 5× 网络往返。
    /// </summary>
    public async Task<ApiResponse<OrderStatusCountDto>> GetStatusCountsAsync()
    {
        // ⚠️ 会员身份必须从 Token 取，绝不信任前端；非会员令牌直接拒绝
        var memberId = _currentUser.MemberId;
        if (memberId <= 0 || !_currentUser.IsMember)
        {
            return ApiResponse<OrderStatusCountDto>.Fail("无法识别当前会员身份", 401);
        }

        // 归属过滤后，按 order_status 分组计数
        var groups = await _db.TrxOrders
            .AsNoTracking()
            .Where(o => o.IsDeleted == 0 && o.BuyerId == memberId)
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var map = groups.ToDictionary(g => g.Status, g => g.Count);
        var dto = new OrderStatusCountDto
        {
            PendingPay = map.GetValueOrDefault(OrderStatuses.Pending),
            PendingShip = map.GetValueOrDefault(OrderStatuses.Paid),
            PendingReceive = map.GetValueOrDefault(OrderStatuses.Shipped),
            Completed = map.GetValueOrDefault(OrderStatuses.Completed),
            Closed = map.GetValueOrDefault(OrderStatuses.Closed)
        };

        return ApiResponse<OrderStatusCountDto>.Success(dto);
    }

    // ============================================================
    // 2. 订单详情
    // ============================================================

    /// <summary>订单详情（主表 + 明细列表 trx_order_item）</summary>
    public async Task<ApiResponse<OrderDetailDto>> GetDetailAsync(long id)
    {
        var order = await _orderRepository.GetDetailAsync(id);
        if (order == null)
        {
            return ApiResponse<OrderDetailDto>.Fail("订单不存在", 404);
        }

        // ⚠️ 归属校验：会员令牌只能查看【自己】的订单；管理员令牌（无 member_id 声明）可查看全部
        if (_currentUser.IsMember && order.BuyerId != _currentUser.MemberId)
        {
            return ApiResponse<OrderDetailDto>.Fail("无权查看该订单", 403);
        }

        var dto = new OrderDetailDto();
        FillListDto(dto, order);
        dto.BuyerName = await _db.MemMembers.AsNoTracking()
            .Where(m => m.Id == order.BuyerId)
            .Select(m => m.Name)
            .FirstOrDefaultAsync();
        dto.ItemCount = order.Items.Count;
        dto.TotalQuantity = order.Items.Sum(i => i.Quantity);
        dto.Items = order.Items.Select(MapToItemDto).ToList();

        return ApiResponse<OrderDetailDto>.Success(dto);
    }

    // ============================================================
    // 3. 创建订单（生成订单号 + 商品快照 + 金额重算 + 事务）
    // ============================================================

    /// <summary>
    /// 创建订单（【先充值后下单】）：
    /// 1) 通过价格策略引擎取价，得到每行商品的最终成交价（取价失败回退 SKU 标准零售价）；
    /// 2) 校验商品/SKU 存在性与归属，数量必须 &gt; 0；
    /// 3) 生成不重复订单号（ORD + yyyyMMddHHmmss + 4位随机数）；
    /// 4) 从 prod_info / prod_sku 读取名称、规格、图片写入明细【快照】；
    /// 5) 后端重算金额（不信任前端）；
    /// 6) 【余额校验 + 冻结】校验会员可用余额 ≥ 应付金额，不足直接拦截（提示先充值）；
    ///    通过后在同一事务内把金额从 balance 转入 frozen_balance，并写 mkt_balance_log；
    /// 7) 【价格快照】写入 order_price_snapshot，作为历史价格追溯与毛利分析依据。
    /// 注意：创建阶段【不扣库存】，库存扣减发生在支付时；冻结额在支付成功时销账（扣减）。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(OrderCreateDto dto)
    {
        // 1) 按价格策略取价（会员未指定或取价异常时回退标准售价，不阻断下单）
        var quotation = await ResolveQuotationAsync(dto);

        // 2) 校验 + 生成明细快照（不通过抛 InvalidOperationException，由 Controller 转 400）
        var items = await BuildItemSnapshotsAsync(dto.Items, quotation);

        // 3) 生成订单号
        var orderNo = await GenerateOrderNoAsync();

        // 4) 后端重算金额（F-31：主动校验，不再「归零」兜底）
        var totalAmount = items.Sum(i => i.SubtotalAmount);
        var payAmount = ValidateOrderAmounts(
            totalAmount, dto.DiscountAmount, dto.FreightAmount, forbidMemberDiscount: _currentUser.IsMember);

        // 5) 校验下单买家（会员）必须存在
        //
        // ⚠️ 口径说明（BUG-05）：trx_order.buyer_id 指向【mem_member.id（会员/客户）】，
        //    与后台管理账号 sys_user 物理隔离（依据：customer_init.sql 建表注释）。
        //    即 customer_invoice_info.customer_id 与 order.buyer_id 本就是同一套 ID。
        //
        // 原缺陷：下单仅在余额校验时"顺带"检查会员；若应付金额为 0（全额优惠）或改用
        // 微信/线下支付，就会写入 buyer_id 指向不存在会员的【孤儿订单】，后续退款执行、
        // 余额扣减、应收生成、开票归属全部报「会员不存在」。
        var buyerExists = await _db.MemMembers.AsNoTracking()
            .AnyAsync(m => m.Id == dto.BuyerId && m.IsDeleted == 0);
        if (!buyerExists)
        {
            return ApiResponse<object>.Fail(
                $"下单买家不存在（buyerId={dto.BuyerId}）：请先在【客户管理】创建该客户（mem_member），且未被删除");
        }

        // 6) 【先充值后下单】余额校验：可用余额必须 ≥ 应付金额
        if (payAmount > 0)
        {
            var check = await _balanceService.CheckEnoughAsync(dto.BuyerId, payAmount);
            if (check.Code != 200)
            {
                return ApiResponse<object>.Fail(check.Message);
            }
        }

        // 7) 事务写入
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new TrxOrder
            {
                OrderNo = orderNo,
                BuyerId = dto.BuyerId,
                ReceiverName = dto.ReceiverName,
                ReceiverPhone = dto.ReceiverPhone,
                ReceiverProvince = dto.ReceiverProvince ?? string.Empty,
                ReceiverCity = dto.ReceiverCity ?? string.Empty,
                ReceiverDistrict = dto.ReceiverDistrict ?? string.Empty,
                ReceiverAddress = dto.ReceiverAddress,
                TotalAmount = totalAmount,
                DiscountAmount = dto.DiscountAmount,
                FreightAmount = dto.FreightAmount,
                PayAmount = payAmount,
                OrderStatus = OrderStatuses.Pending,   // 初始状态：待付款
                PayMethod = dto.PayMethod,
                BuyerRemark = dto.BuyerRemark ?? string.Empty,
                MerchantRemark = dto.MerchantRemark ?? string.Empty,
                IsDeleted = 0
            };

            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();   // 先落主表以拿到自增 order.Id

            foreach (var item in items)
            {
                item.OrderId = order.Id;
            }
            await _itemRepository.AddRangeAsync(items);
            await _db.SaveChangesAsync();

            // 8) 累加商品销量（prod_info.sales_count）：与订单创建同事务，订单失败回滚则不累加
            await IncrementProductSalesAsync(items);

            // 7) 【资金冻结】可用余额 → 冻结余额（与订单同事务，失败整体回滚）
            if (payAmount > 0)
            {
                await _balanceService.FreezeForOrderAsync(
                    dto.BuyerId, payAmount, order.Id, $"下单冻结-{order.OrderNo}");

                // 【阶段九】在订单上留痕"本单到底冻结了多少"。
                // frozen_balance 是会员级共享池，没有这笔留痕，取消时只能拿应付金额去猜，
                // 会误退别的订单的冻结额（详见 trx_order.frozen_amount 注释）。
                order.FrozenAmount = payAmount;
            }

            // 8) 【价格快照】冻结下单瞬间的成交价与命中策略
            await SavePriceSnapshotsAsync(order.Id, items, quotation);

            // 9) 【财务结算联动】创建订单即生成应收账款（未结清），
            //    让「收款记录 → 执行核销」能挂靠到该订单；在线/余额支付时由 PayAsync 自动结清。
            await _receivableService.SyncFromOrderAsync(order.Id);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(
                new { id = order.Id, orderNo = order.OrderNo, payAmount = order.PayAmount },
                payAmount > 0 ? "订单创建成功，余额已冻结，待付款" : "订单创建成功，待付款");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 调用价格策略引擎取价，返回「SKU ID → 取价结果」映射。
    /// 取价失败（会员不存在、无策略、参数异常等）时返回空映射，下单流程回退到 SKU 标准零售价，
    /// 保证价格模块异常不影响下单主链路。
    /// </summary>
    private async Task<Dictionary<long, QuotationResultDto>> ResolveQuotationAsync(OrderCreateDto dto)
    {
        var map = new Dictionary<long, QuotationResultDto>();
        if (dto.BuyerId <= 0 || dto.Items.Count == 0)
        {
            return map;
        }

        try
        {
            var request = new QuotationRequestDto
            {
                MemMemberId = dto.BuyerId,
                Items = dto.Items
                    .Select(i => new QuotationItemInputDto { MaterialId = i.SkuId, Quantity = i.Quantity })
                    .ToList()
            };

            var resp = await _priceService.QuotationAsync(request);
            if (resp.Code == 200 && resp.Data != null)
            {
                foreach (var r in resp.Data)
                {
                    map[r.MaterialId] = r;
                }
            }
        }
        catch
        {
            // 取价异常不阻断下单：回退标准售价（价格策略是增值能力，不是下单前置条件）
        }

        return map;
    }

    /// <summary>
    /// 批量写入订单价格快照（order_price_snapshot）。
    /// 命中价格策略的行写入「标准售价 + 命中策略/规则 + 最终成交价 + 折扣详情」；
    /// 未命中的行也要落库（final_price = standard_price），保证每笔订单的快照完整可追溯。
    /// </summary>
    private async Task SavePriceSnapshotsAsync(
        long orderId, List<TrxOrderItem> items, Dictionary<long, QuotationResultDto> quotation)
    {
        foreach (var item in items)
        {
            if (quotation.TryGetValue(item.SkuId, out var q))
            {
                await _db.OrderPriceSnapshots.AddAsync(new OrderPriceSnapshot
                {
                    OrderId = orderId,
                    OrderItemId = item.Id,
                    MaterialId = item.SkuId,
                    ProdInfoId = item.ProdInfoId,   // 数据库该列 NOT NULL，直接取订单明细快照
                    StandardPrice = q.StandardPrice,
                    MatchedStrategyId = q.MatchedStrategyId,
                    MatchedStrategyType = q.MatchedStrategyType,
                    MatchedRuleId = q.MatchedRuleId,
                    FinalPrice = q.FinalPrice,
                    DiscountInfo = PriceService.SerializeDiscountInfo(q.DiscountInfo)
                });
            }
            else
            {
                await _db.OrderPriceSnapshots.AddAsync(new OrderPriceSnapshot
                {
                    OrderId = orderId,
                    OrderItemId = item.Id,
                    MaterialId = item.SkuId,
                    ProdInfoId = item.ProdInfoId,   // 数据库该列 NOT NULL
                    StandardPrice = item.UnitPrice,
                    MatchedStrategyId = 0,
                    MatchedStrategyType = 0,
                    MatchedRuleId = 0,
                    FinalPrice = item.UnitPrice,
                    DiscountInfo = PriceService.SerializeDiscountInfo(new DiscountInfoDto
                    {
                        StandardPrice = item.UnitPrice,
                        FinalPrice = item.UnitPrice,
                        DiscountAmount = 0m,
                        Remark = "未命中价格策略，按商品标准售价"
                    })
                });
            }
        }
    }

    // ============================================================
    // 4. 支付（待付款 → 已支付）：【扣减 SKU 库存】
    // ============================================================

    /// <summary>
    /// 订单支付（无审核，支付即生效）：
    /// 状态 Pending(0) → Paid(1)，同时按明细逐条扣减 SKU 库存并写变动日志。
    /// 全程事务：任一 SKU 库存不足则整体回滚，不会出现"钱扣了库存没扣"的中间态。
    /// </summary>
    public async Task<ApiResponse<object>> PayAsync(long id, OrderPayDto dto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        // 【阶段六】归属校验：会员令牌只能支付自己的订单
        if (!CanAccessOrder(order))
        {
            return ApiResponse<object>.Fail("无权操作该订单", 403);
        }

        // 【状态校验】只有待付款才能支付
        if (order.OrderStatus != OrderStatuses.Pending)
        {
            return ApiResponse<object>.Fail("仅待付款订单可支付（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        var items = await _itemRepository.GetByOrderIdAsync(id);
        if (items.Count == 0)
        {
            return ApiResponse<object>.Fail("订单无明细，无法支付");
        }

        var logs = new List<LogStockLog>();
        var stockCache = new Dictionary<long, int>();

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 并发保护：用条件更新抢占「待付款 → 已支付」，抢不到说明已被其他请求处理
            if (!await TryTransitionAsync(id, OrderStatuses.Pending, OrderStatuses.Paid))
            {
                await tx.RollbackAsync();
                return ApiResponse<object>.Fail("订单状态已变更，请刷新后重试");
            }

            // 【阶段八】余额支付：与 SubmitAsync 统一加锁顺序 —— 先锁会员余额行，再碰 SKU。
            // 两条链路若顺序相反（一条 会员→SKU、一条 SKU→会员），并发时会在 InnoDB 里
            // 互相等待对方的行锁而死锁（1213），只能靠数据库回滚一方，用户看到 500。
            // 这里金额传 0 表示"只加锁、不校验额度"（冻结额在下面 DeductForOrderAsync 里校验）。
            //
            // ⚠️ 现状：OrderPayDto.PayMethod 带 [Range(1,3)]，余额支付(4) 在本接口会被直接拒绝，
            //    因此下面这段分支目前不会命中——「余额支付」的唯一入口是 POST /order/submit。
            //    保留它是为了将来放开 payMethod=4 时（例如让后台待付款单改用余额结账）不必再改这里。
            if (order.PayAmount > 0 && dto.PayMethod == PayMethods.Balance)
            {
                await _balanceService.LockBalanceForPaymentAsync(order.BuyerId, 0m);
            }

            // 逐条扣减库存（delta 为负数）
            foreach (var item in items)
            {
                await DeductStockAsync(
                    logs, stockCache,
                    item.ProdInfoId, item.SkuId, -item.Quantity,
                    $"订单支付扣减-{order.OrderNo}", id);
            }

            // 状态推进：待付款 → 已支付（待发货）
            order.OrderStatus = OrderStatuses.Paid;
            order.PayMethod = dto.PayMethod;
            order.PayTime = DateTime.Now;

            // 【财务结算联动】在线/余额支付成功 → 结清该订单的应收账款。
            // 订单创建时已生成应收，而这笔钱不经「收款单核销」进来，必须在此直接结清，
            // 否则会残留一条永远收不回的应收（脏数据）。
            await _receivableService.SettleByOrderAsync(order.Id);

            // 【联动客户统计】订单支付即视为"有效订单"：累计消费 + 订单数 + 最近订单回写。
            // buyer_id 即 mem_member.id；客户不存在（如历史脏数据）时跳过，不影响主流程。
            await UpdateMemberOnPaidAsync(order.BuyerId, order.PayAmount, order.OrderNo);

            // 【先充值后下单 · 第 4 步】支付成功 → 把下单时冻结的金额真正划走（frozen_balance -=）。
            // ⚠️ 仅对「余额支付(4)」订单执行：银行转账/货到付款等线下待付款订单在下单时
            //    并未冻结余额（见 SubmitAsync 待付款分支），若强行走冻结划扣会因"冻结不足"抛异常，
            //    导致后台对线下订单点「支付」时失败。
            if (order.PayAmount > 0 && dto.PayMethod == PayMethods.Balance)
            {
                // 【阶段九】按本单实际冻结额决定扣款来源（不再按会员冻结池猜测）：
                //   本单有冻结 → 扣冻结额；本单没冻结（会员自助下单从不冻结）→ 直接扣可用余额。
                if (order.FrozenAmount > 0)
                {
                    await _balanceService.DeductForOrderAsync(
                        order.BuyerId, order.FrozenAmount, order.Id, $"订单支付扣减-{order.OrderNo}");
                    order.FrozenAmount = 0m;   // 冻结已转为实际扣款，归零防重复
                }
                else
                {
                    await _balanceService.DeductDirectForOrderAsync(
                        order.BuyerId, order.PayAmount, order.Id, $"订单支付扣减-{order.OrderNo}");
                }
            }

            await _db.LogStockLogs.AddRangeAsync(logs);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(
                new { id = id, orderNo = order.OrderNo, orderStatus = order.OrderStatus },
                "支付成功，库存已扣减");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 4.5 收款核销联动：应收结清 → 订单置为已支付
    // ============================================================

    /// <summary>
    /// 【收款核销联动】某订单的应收账款被核销至未收 = 0 后，把订单从「待付款」推进为「已支付」。
    ///
    /// 与 PayAsync 的差异：线下收款（现金/转账）不经过会员余额，
    /// 因此这里**不扣冻结余额**；其余动作（扣减库存、更新客户累计消费/订单数、写库存日志）与支付一致。
    ///
    /// 【事务】刻意不自开事务：由 PaymentService.WriteOffAsync 的核销事务统一提交，
    ///         保证"核销明细 + 应收回写 + 收款单状态 + 订单状态 + 库存"整体原子。
    /// 【幂等】订单不存在 / 已删除 / 非「待付款」时直接返回，不重复扣库存。
    /// </summary>
    public async Task MarkPaidFromWriteOffAsync(long orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null || order.IsDeleted == 1) return;
        if (order.OrderStatus != OrderStatuses.Pending) return;   // 仅待付款需要推进

        var items = await _itemRepository.GetByOrderIdAsync(orderId);
        if (items.Count == 0) return;

        var logs = new List<LogStockLog>();
        var stockCache = new Dictionary<long, int>();

        // 与订单支付一致：逐条扣减库存（delta 为负）
        foreach (var item in items)
        {
            await DeductStockAsync(
                logs, stockCache,
                item.ProdInfoId, item.SkuId, -item.Quantity,
                $"收款核销结清-{order.OrderNo}", orderId);
        }

        // 状态推进：待付款 → 已支付（待发货）
        order.OrderStatus = OrderStatuses.Paid;
        order.PayTime = DateTime.Now;
        // 线下收款默认记为「银行转账」（原为 0-未支付时）
        if (order.PayMethod == PayMethods.Unpaid)
        {
            order.PayMethod = PayMethods.BankTransfer;
        }

        // 【阶段九】线下收款结清 → 本单冻结的余额必须释放回可用余额。
        //   这笔钱是客户用银行转账/现金付的，会员余额里被冻结的那部分不该被"消费"；
        //   不释放就会永久卡在 frozen_balance（原缺口：取消会解冻，正常收款反而不解冻）。
        //   与订单状态推进同在调用方的核销事务里，任一失败整体回滚。
        if (order.FrozenAmount > 0)
        {
            await _balanceService.UnfreezeForOrderAsync(
                order.BuyerId, order.FrozenAmount, orderId, $"收款核销解冻-{order.OrderNo}");
            order.FrozenAmount = 0m;
        }

        // 客户统计联动（累计消费 + 订单数 + 最近订单）
        await UpdateMemberOnPaidAsync(order.BuyerId, order.PayAmount, order.OrderNo);

        if (logs.Count > 0)
        {
            await _db.LogStockLogs.AddRangeAsync(logs);
        }
        await _db.SaveChangesAsync();
    }

    // ============================================================
    // 5. 手动发货（已支付 → 已发货）
    // ============================================================

    /// <summary>
    /// 手动发货：状态 Paid(1) → Shipped(2)，写入物流公司与物流单号。
    /// 【状态校验】只有已支付（已扣库存）的订单才能发货，待付款订单未扣库存不允许发货；
    /// 已发货订单不可重复发货。
    /// 本操作只更新订单主表（不涉及库存/销量），但按约束同样放在事务内保证原子性。
    /// </summary>
    public async Task<ApiResponse<object>> ShipAsync(long id, OrderShipDto dto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        // 【状态校验】只有已支付才能发货
        if (order.OrderStatus != OrderStatuses.Paid)
        {
            return ApiResponse<object>.Fail("仅已支付订单可发货（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 并发保护：用条件更新抢占「已支付 → 已发货」，防止重复发货
            if (!await TryTransitionAsync(id, OrderStatuses.Paid, OrderStatuses.Shipped))
            {
                await tx.RollbackAsync();
                return ApiResponse<object>.Fail("订单状态已变更，请刷新后重试");
            }

            order.ShipCompany = dto.ShipCompany;
            order.ShipNo = dto.ShipNo;
            // 物流面单照片：覆盖写入（允许重新发货时更新/清空）
            order.ShippingImageUrl = (dto.ShippingImageUrl ?? string.Empty).Trim();
            order.OrderStatus = OrderStatuses.Shipped;   // 已发货（待收货）

            // 【财务结算联动】信用订单（is_credit=1）发货后自动生成应收账款。
            // 放在事务内：应收生成失败则发货一并回滚，保证"发了货就一定能收到账"。
            // 内部已做幂等（同一订单只生成一条），非信用订单直接返回 0，不产生应收。
            if (order.IsCredit == 1)
            {
                await _receivableService.SyncFromOrderAsync(order.Id);
            }

            await _orderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(
                new { id = id, orderNo = order.OrderNo, shipCompany = order.ShipCompany, shipNo = order.ShipNo, shippingImageUrl = order.ShippingImageUrl },
                "发货成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 6. 确认完成（已发货 → 已完成）：【增加 SKU 销量】
    // ============================================================

    /// <summary>
    /// 确认完成：状态 Shipped(2) → Completed(3)，按明细逐条增加 SKU 销量（prod_sku.sales_count）。
    /// 【重要】此步只加销量、【不再扣库存】——库存已在支付环节扣减过，避免重复扣减。
    /// 销量累加是幂等无害的加法，不需要 CAS 乐观锁，直接读改存即可。
    /// </summary>
    public async Task<ApiResponse<object>> CompleteAsync(long id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        // 【阶段六】归属校验：会员令牌只能确认自己的订单收货
        if (!CanAccessOrder(order))
        {
            return ApiResponse<object>.Fail("无权操作该订单", 403);
        }

        // 【状态校验】只有已发货才能确认完成
        if (order.OrderStatus != OrderStatuses.Shipped)
        {
            return ApiResponse<object>.Fail("仅已发货订单可确认完成（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        var items = await _itemRepository.GetByOrderIdAsync(id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 并发保护：用条件更新抢占「已发货 → 已完成」，防止销量被重复累加
            if (!await TryTransitionAsync(id, OrderStatuses.Shipped, OrderStatuses.Completed))
            {
                await tx.RollbackAsync();
                return ApiResponse<object>.Fail("订单状态已变更，请刷新后重试");
            }

            // 批量取出涉及 SKU（跟踪状态，直接改字段后 SaveChanges 提交）
            var skuIds = items.Select(i => i.SkuId).Distinct().ToList();
            var skus = await _db.ProdSkus.Where(s => skuIds.Contains(s.Id)).ToListAsync();
            var skuMap = skus.ToDictionary(s => s.Id);

            foreach (var item in items)
            {
                if (skuMap.TryGetValue(item.SkuId, out var sku))
                {
                    // sales_count 是可空列，历史数据可能为 NULL，故按 0 处理
                    sku.SalesCount = (sku.SalesCount ?? 0) + item.Quantity;
                }
            }

            order.OrderStatus = OrderStatuses.Completed;   // 已完成（终态）
            order.CompleteTime = DateTime.Now;

            // 【联动客户统计】订单完成：已完成订单数 +1（累计消费/订单数已在支付时计入，避免重复统计）。
            await UpdateMemberOnCompletedAsync(order.BuyerId);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(
                new { id = id, orderNo = order.OrderNo },
                "订单已完成，销量已累加");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 7. 关闭订单（待付款 → 已关闭）
    // ============================================================

    /// <summary>
    /// 关闭订单：状态 Pending(0) → Closed(4)，记录关闭原因与时间。
    /// 【状态校验】只有待付款可关闭；已支付订单需走退款流程（本模块不处理退款）。
    /// 关闭不涉及库存回滚（库存本来就没扣）。
    /// 【资金】下单时冻结的余额同步解冻退回可用余额（frozen → balance），并写解冻流水。
    /// </summary>
    public async Task<ApiResponse<object>> CancelAsync(long id, OrderCloseDto dto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        // 【阶段六】归属校验：会员令牌只能关闭自己的订单（否则可恶意取消他人订单）
        if (!CanAccessOrder(order))
        {
            return ApiResponse<object>.Fail("无权操作该订单", 403);
        }

        if (order.OrderStatus != OrderStatuses.Pending)
        {
            return ApiResponse<object>.Fail("仅待付款订单可关闭（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 并发保护：用条件更新抢占「待付款 → 已关闭」，避免与支付并发时双双成功
            if (!await TryTransitionAsync(id, OrderStatuses.Pending, OrderStatuses.Closed))
            {
                await tx.RollbackAsync();
                return ApiResponse<object>.Fail("订单状态已变更，请刷新后重试");
            }

            order.OrderStatus = OrderStatuses.Closed;
            order.CloseReason = dto.CloseReason ?? string.Empty;
            order.CloseTime = DateTime.Now;

            // ============================================================
            // 【阶段九·用户选项 A】关闭订单必须**无条件**解冻本单资金
            //
            //   冻结金额取 order.FrozenAmount（本单冻结时留的痕），不再用应付金额去猜：
            //   frozen_balance 是会员级共享池，若用 min(应付, 池子) 释放，
            //   取消一张【从未冻结】的单会把【别的单】的冻结额退掉。
            //
            //   与「订单状态改为已关闭」在**同一个事务**里：
            //   任一步失败 → 整体回滚（订单仍是待付款、钱仍冻着），
            //   绝不会出现「订单关了、钱没退」或「钱退了、订单还开着」。
            // ============================================================
            if (order.FrozenAmount > 0)
            {
                await _balanceService.UnfreezeForOrderAsync(
                    order.BuyerId, order.FrozenAmount, order.Id, $"订单关闭解冻-{order.OrderNo}");

                // 释放后立即归零：同一订单无论重复取消还是后续支付，都不会二次释放
                order.FrozenAmount = 0m;
            }

            await _orderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id, orderNo = order.OrderNo }, "订单已关闭，冻结余额已退回");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 8. 订单预计算（确认页初始化）
    // ============================================================

    /// <summary>
    /// 订单预计算（GET/POST /api/order/pre-calculate）：
    /// 后端重算每行单价（命中价格策略取成交价，否则 SKU 零售价）、小计与应付金额，
    /// 并【校验库存】（不足时逐行给出提示、整体 stockOk=false）。
    /// 金额不信任前端：total = Σ(单价×数量)，pay = total - discount + freight。
    /// 买家取当前登录会员（JWT），保证与提交时一致。
    /// </summary>
    public async Task<ApiResponse<OrderPreCalculateResultDto>> PreCalculateAsync(OrderPreCalculateDto dto)
    {
        var memberId = _currentUser.MemberId;
        var quotation = await ResolveQuotationByMemberAsync(memberId, dto.Items);

        // 构建快照（校验商品/SKU 存在、数量>0；失败直接 Fail，不抛异常）
        List<TrxOrderItem> items;
        try
        {
            items = await BuildSubmitItemsAsync(dto.Items, quotation);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<OrderPreCalculateResultDto>.Fail(ex.Message);
        }

        // 一次性读取涉及 SKU 的当前库存
        var skuIds = items.Select(i => i.SkuId).Distinct().ToList();
        var stockMap = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Stock);

        var totalAmount = items.Sum(i => i.SubtotalAmount);

        // ===== F-31：预计算同样必须拦截「优惠 > 总额」=====
        // 否则确认页会直接渲染出 0 元应付，用户点提交即触发 0 元下单。
        var discount = dto.DiscountAmount;
        var freight = dto.FreightAmount;
        var payAmount = ValidateOrderAmounts(
            totalAmount, discount, freight, forbidMemberDiscount: _currentUser.IsMember);

        var result = new OrderPreCalculateResultDto
        {
            TotalAmount = totalAmount,
            DiscountAmount = discount,
            FreightAmount = freight,
            PayAmount = payAmount
        };

        var allStockOk = true;
        foreach (var it in items)
        {
            var stock = stockMap.TryGetValue(it.SkuId, out var st) ? st : 0;
            var ok = stock >= it.Quantity;
            if (!ok)
            {
                allStockOk = false;
                result.StockMessage = $"商品「{it.ProdName}」库存不足（剩余 {stock}，需 {it.Quantity}）";
            }

            result.Items.Add(new OrderPreCalculateItemDto
            {
                SkuId = it.SkuId,
                ProdName = it.ProdName,
                SkuName = it.SkuName,
                SpecValues = it.SpecValues,
                ProductImage = it.ProductImage,
                UnitPrice = it.UnitPrice,
                Quantity = it.Quantity,
                Subtotal = it.SubtotalAmount,
                StockOk = ok,
                StockMessage = ok ? null : $"库存不足（剩余 {stock}）"
            });
        }

        // TotalAmount / DiscountAmount / FreightAmount / PayAmount 已在构建 result 时统一校验并赋值，
        // 此处不再重算，彻底移除「payAmount < 0 就归零」的被动兜底。
        result.StockOk = allStockOk;

        return ApiResponse<OrderPreCalculateResultDto>.Success(result);
    }

    // ============================================================
    // 9. 订单提交（确认页「提交订单」——单事务：库存校验 → 建单 → 余额直扣 → 置已支付）
    // ============================================================

    /// <summary>
    /// 订单提交（POST /api/order/submit，余额支付专用）：
    /// 在【同一个数据库事务】内完成：
    ///   1) 解析收货信息（优先已保存地址 AddressId，否则内联 Receivers + 可选 SaveAddress）；
    ///   2) 后端重算金额（不信任前端）；
    ///   3) 创建订单（状态=待付款 Pending）；
    ///   4) 【再次校验并扣减库存】（CAS，库存不足整体回滚）；
    ///   5) 【立即余额直扣】（可用余额 → 扣减，写 OrderDeduct 流水；不足抛异常回滚）；
    ///   6) 订单状态推进为已支付 Paid + 写 pay_time + 会员统计联动 + 价格快照；
    ///   7) 提交事务。
    /// 任一步失败整体回滚，绝不出现「钱扣了库存没扣 / 单建了钱没扣」的中间态。
    /// </summary>
    public async Task<ApiResponse<OrderSubmitResultDto>> SubmitAsync(OrderSubmitDto dto)
    {
        var buyerId = _currentUser.MemberId;
        if (buyerId <= 0)
        {
            return ApiResponse<OrderSubmitResultDto>.Fail("未获取到登录会员信息，请重新登录", 401);
        }

        // 校验买家会员必须存在（避免孤儿订单）
        var buyerExists = await _db.MemMembers.AsNoTracking()
            .AnyAsync(m => m.Id == buyerId && m.IsDeleted == 0);
        if (!buyerExists)
        {
            return ApiResponse<OrderSubmitResultDto>.Fail($"下单会员不存在（memberId={buyerId}）");
        }

        // ---------- 1) 解析收货信息 ----------
        string receiverName, receiverPhone, receiverProvince, receiverCity, receiverDistrict, receiverAddress;
        if (dto.AddressId > 0)
        {
            var addr = await _db.MemMemberAddresses
                .FirstOrDefaultAsync(a => a.Id == dto.AddressId && a.MemberId == buyerId && a.IsDeleted == 0);
            if (addr == null)
            {
                return ApiResponse<OrderSubmitResultDto>.Fail("收货地址不存在或不属于当前会员");
            }

            receiverName = addr.Name;
            receiverPhone = addr.Phone;
            receiverProvince = addr.Province;
            receiverCity = addr.City;
            receiverDistrict = addr.District;
            receiverAddress = addr.Detail;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.ReceiverName)
                || string.IsNullOrWhiteSpace(dto.ReceiverPhone)
                || string.IsNullOrWhiteSpace(dto.ReceiverAddress))
            {
                return ApiResponse<OrderSubmitResultDto>.Fail("请填写完整的收货信息（姓名/电话/详细地址）");
            }

            receiverName = dto.ReceiverName.Trim();
            receiverPhone = dto.ReceiverPhone.Trim();
            receiverProvince = dto.ReceiverProvince ?? string.Empty;
            receiverCity = dto.ReceiverCity ?? string.Empty;
            receiverDistrict = dto.ReceiverDistrict ?? string.Empty;
            receiverAddress = dto.ReceiverAddress.Trim();

            // 立即下单且要求保存：落库为新地址（默认地址逻辑由仓储/Controller 的 save 接口保证，
            // 这里仅简单保存，不处理默认位互斥，避免与 /api/address/save 重复）
            if (dto.SaveAddress)
            {
                var newAddr = new MemMemberAddress
                {
                    MemberId = buyerId,
                    Name = receiverName,
                    Phone = receiverPhone,
                    Province = receiverProvince,
                    City = receiverCity,
                    District = receiverDistrict,
                    Detail = receiverAddress,
                    IsDefault = 0,
                    IsDeleted = 0
                };
                await _db.MemMemberAddresses.AddAsync(newAddr);
                await _db.SaveChangesAsync();
            }
        }

        // ---------- 2) 后端重算金额 + 构建商品快照 ----------
        var quotation = await ResolveQuotationByMemberAsync(buyerId, dto.Items);

        List<TrxOrderItem> items;
        try
        {
            items = await BuildSubmitItemsAsync(dto.Items, quotation);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<OrderSubmitResultDto>.Fail(ex.Message);
        }

        var totalAmount = items.Sum(i => i.SubtotalAmount);

        // ===== F-31【主动校验】替代原先「先算再归零」的被动防御 =====
        // 优惠金额是前端唯一可自由传入的金额（单价由价格引擎重算，优惠没有），
        // 必须用【服务端自算的 totalAmount】裁决上限；否则可被放大到总额之上，
        // 把应付压成 0，却仍然扣库存 / 置已支付 / 结清应收 → 0 元拿货。
        var discount = dto.DiscountAmount;
        var freight = dto.FreightAmount;
        var payAmount = ValidateOrderAmounts(
            totalAmount, discount, freight, forbidMemberDiscount: _currentUser.IsMember);

        var orderNo = await GenerateOrderNoAsync();
        var logs = new List<LogStockLog>();
        var stockCache = new Dictionary<long, int>();

        // 余额支付（4）= 提交即扣款；其余（银行转账/货到付款）生成待付款订单
        var isInstantPay = dto.PayMethod == PayMethods.Balance;

        // ---------- 3)~7) 单事务 ----------
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // ============================================================
            // ①【阶段八】余额支付：事务内第一步锁定余额行并校验充足性
            //
            // 【为什么放最前】
            //   顺序：锁余额 → 建订单 → 扣库存 → 扣余额 → 置已支付。
            //   余额不足时在这里就抛出，不会白建订单/白扣库存/白写价格快照，
            //   也就不会为了一个注定失败的单去占着 SKU 行锁。
            //
            // 【为什么必须显式加锁】
            //   同一会员并发提交两单时，两个事务若都"读余额→判断够→各自扣"，
            //   就会花掉不存在的钱（超扣）。SELECT ... FOR UPDATE 让同一会员的
            //   余额支付串行化，锁随本事务提交/回滚释放。
            // ============================================================
            if (isInstantPay && payAmount > 0)
            {
                await _balanceService.LockBalanceForPaymentAsync(buyerId, payAmount);
            }

            var order = new TrxOrder
            {
                OrderNo = orderNo,
                BuyerId = buyerId,
                ReceiverName = receiverName,
                ReceiverPhone = receiverPhone,
                ReceiverProvince = receiverProvince,
                ReceiverCity = receiverCity,
                ReceiverDistrict = receiverDistrict,
                ReceiverAddress = receiverAddress,
                TotalAmount = totalAmount,
                DiscountAmount = discount,
                FreightAmount = freight,
                PayAmount = payAmount,
                OrderStatus = OrderStatuses.Pending,   // 先建待支付：余额支付扣款成功后转已支付；线下付款则保持待付款
                PayMethod = dto.PayMethod,             // 由前端选择：余额支付(4) / 银行转账(2) / 货到付款(3)
                BuyerRemark = dto.Remark ?? string.Empty,
                MerchantRemark = string.Empty,
                IsDeleted = 0
            };

            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();   // 先落主表拿 order.Id

            foreach (var it in items)
            {
                it.OrderId = order.Id;
            }
            await _itemRepository.AddRangeAsync(items);
            await _db.SaveChangesAsync();

            // 8) 累加商品销量（prod_info.sales_count）：与订单创建同事务，订单失败回滚则不累加；
            //    按商品聚合本次下单数量，数据库级原子自增，天然不重复（同订单只建一次）。
            await IncrementProductSalesAsync(items);

            // ============================================================
            // 【支付方式分支】
            //   余额支付(4)      → 下单即付：扣库存 + 扣余额 + 置已支付；
            //   银行转账(2)/货到付款(3) → 生成【待付款】订单：不扣库存、不扣余额，
            //                        等后台录入收款单 → 执行核销 → 应收结清时
            //                        由 MarkPaidFromWriteOffAsync 扣库存并转「已支付」。
            // ============================================================

            // 价格快照（历史价格追溯）：两种方式都要留档
            await SavePriceSnapshotsAsync(order.Id, items, quotation);

            if (isInstantPay)
            {
                // 4) 再次校验并扣减库存（CAS；不足 → 抛异常 → 整体回滚）
                foreach (var it in items)
                {
                    await DeductStockAsync(
                        logs, stockCache,
                        it.ProdInfoId, it.SkuId, -it.Quantity,
                        $"订单支付扣减-{order.OrderNo}", order.Id);
                }

                // 5) 立即余额直扣（可用余额 → 扣减；不足 → 抛异常 → 整体回滚）
                if (payAmount > 0)
                {
                    await _balanceService.DeductDirectForOrderAsync(
                        buyerId, payAmount, order.Id, $"订单支付扣减-{order.OrderNo}");
                }

                // 6) 订单状态推进为已支付
                order.OrderStatus = OrderStatuses.Paid;
                order.PayTime = DateTime.Now;

                // 会员统计联动（累计消费 + 订单数 + 最近订单）
                await UpdateMemberOnPaidAsync(buyerId, payAmount, order.OrderNo);
            }
            // else：待付款订单 —— 不动库存、不动余额、客户统计等核销后由
            //       MarkPaidFromWriteOffAsync 统一计入（与"支付"时点保持一致）。

            // 【财务结算联动】两种方式都要生成应收台账：
            await _receivableService.SyncFromOrderAsync(order.Id);
            if (isInstantPay)
            {
                // 即时支付：这笔钱不经「收款单核销」进来，必须立即结清，避免残留永远收不回的应收。
                await _receivableService.SettleByOrderAsync(order.Id);
            }
            // 待付款：应收保持「未结清」，供「收款记录 → 执行核销」挂靠。

            // 库存变动日志落库（待付款分支 logs 为空，AddRange 空集合无副作用）
            await _db.LogStockLogs.AddRangeAsync(logs);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            // 扣款后会员可用余额（供前端刷新展示）
            var balanceAfter = await _db.MemMembers.AsNoTracking()
                .Where(m => m.Id == buyerId)
                .Select(m => m.Balance)
                .FirstAsync();

            return ApiResponse<OrderSubmitResultDto>.Success(new OrderSubmitResultDto
            {
                Id = order.Id,
                OrderNo = order.OrderNo,
                PayAmount = order.PayAmount,
                BalanceAfter = balanceAfter,
                OrderStatus = order.OrderStatus
            }, isInstantPay ? "下单并支付成功" : "下单成功，订单待付款");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse<OrderSubmitResultDto>.Fail(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 8. 修改备注（仅待付款可改）
    // ============================================================

    /// <summary>
    /// 修改商家备注（merchant_remark）。
    /// 【状态校验】只有 待付款 Pending(0) 才能修改备注（与"修改收货地址"同一规则）：
    /// 订单一旦支付即进入履约流程，再改备注/地址会造成发货与对账歧义。
    /// </summary>
    public async Task<ApiResponse<object>> UpdateRemarkAsync(long id, OrderRemarkDto dto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        if (order.OrderStatus != OrderStatuses.Pending)
        {
            return ApiResponse<object>.Fail("仅待付款订单可修改备注（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        order.MerchantRemark = dto.MerchantRemark;
        await _orderRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id, merchantRemark = order.MerchantRemark }, "备注已更新");
    }

    // ============================================================
    // 9. 订单导出（NPOI 生成真实 Excel）
    // ============================================================

    /// <summary>
    /// 订单导出：按与列表完全相同的筛选条件生成 .xlsx 文件。
    /// 单次导出上限 5000 行，避免全表导出把服务内存与数据库打满。
    /// 金额一律取库中快照字段（pay_amount / total_amount 是下单时持久化的值），不在此重算。
    /// </summary>
    public async Task<(byte[] Content, string FileName)> ExportAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime,
        string? buyerKeyword, string? receiverKeyword)
    {
        // 先取命中总数，再一次性拉取（上限 5000）
        var (_, total) = await _orderRepository.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, null, buyerKeyword, receiverKeyword, 1, 1);
        var take = Math.Max(1, Math.Min(total, 5000));
        var (orders, _) = await _orderRepository.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, null, buyerKeyword, receiverKeyword, 1, take);

        var list = orders.Select(MapToListDto).ToList();

        // 买家名称批量回填（单次查询，不做 N+1）
        var buyerIds = list.Select(o => o.BuyerId).Distinct().ToList();
        if (buyerIds.Count > 0)
        {
            var buyerNames = await _db.MemMembers.AsNoTracking()
                .Where(m => buyerIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);
            foreach (var dto in list)
            {
                dto.BuyerName = buyerNames.GetValueOrDefault(dto.BuyerId);
            }
        }

        IWorkbook workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("订单列表");

        var headerStyle = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        headerStyle.SetFont(font);

        var headers = new[]
        {
            "订单号", "买家", "收货人", "收货人电话", "商品种类", "商品件数",
            "商品总额", "优惠金额", "运费", "实付金额", "订单状态", "支付方式", "下单时间"
        };
        var headerRow = sheet.CreateRow(0);
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }

        for (var r = 0; r < list.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            var o = list[r];
            row.CreateCell(0).SetCellValue(o.OrderNo);
            row.CreateCell(1).SetCellValue(o.BuyerName ?? $"买家#{o.BuyerId}");
            row.CreateCell(2).SetCellValue(o.ReceiverName);
            row.CreateCell(3).SetCellValue(o.ReceiverPhone);
            row.CreateCell(4).SetCellValue(o.ItemCount);
            row.CreateCell(5).SetCellValue(o.TotalQuantity);
            row.CreateCell(6).SetCellValue(o.TotalAmount.ToString("0.00"));
            row.CreateCell(7).SetCellValue(o.DiscountAmount.ToString("0.00"));
            row.CreateCell(8).SetCellValue(o.FreightAmount.ToString("0.00"));
            row.CreateCell(9).SetCellValue(o.PayAmount.ToString("0.00"));
            row.CreateCell(10).SetCellValue(o.OrderStatusName ?? string.Empty);
            row.CreateCell(11).SetCellValue(o.PayMethodName ?? string.Empty);
            row.CreateCell(12).SetCellValue(o.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: true);
        var fileName = $"订单列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    /// <summary>
    /// 校验明细并生成商品快照。
    /// 校验项：商品存在、SKU 存在、SKU 归属该商品、数量 &gt; 0。
    /// 快照字段（prod_name / sku_name / spec_values / product_image / unit_price）
    /// 一律从 prod_info、prod_sku 实时读取写入，保证商品后续改价改名不影响历史订单。
    /// 【单价口径】命中价格策略时取最终成交价（QuotationResultDto.FinalPrice），
    /// 否则取 SKU 零售价；两者都会同步写入 order_price_snapshot。
    /// 不通过时抛 InvalidOperationException（中文消息），由 Controller 统一转 400。
    /// </summary>
    private async Task<List<TrxOrderItem>> BuildItemSnapshotsAsync(
        List<OrderItemInputDto> inputs, Dictionary<long, QuotationResultDto> quotation)
    {
        var prodIds = inputs.Select(i => i.ProdInfoId).Distinct().ToList();
        var skuIds = inputs.Select(i => i.SkuId).Distinct().ToList();

        var prodMap = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var skuMap = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var items = new List<TrxOrderItem>();

        foreach (var input in inputs)
        {
            if (!prodMap.TryGetValue(input.ProdInfoId, out var prod))
            {
                throw new InvalidOperationException($"商品不存在（prod_info_id={input.ProdInfoId}）");
            }
            if (!skuMap.TryGetValue(input.SkuId, out var sku))
            {
                throw new InvalidOperationException($"SKU不存在（sku_id={input.SkuId}）");
            }
            // 防止前端把别的商品的 SKU 挂到本订单上
            if (sku.ProdInfoId != input.ProdInfoId)
            {
                throw new InvalidOperationException(
                    $"SKU与商品不匹配（sku_id={input.SkuId} 不属于 prod_info_id={input.ProdInfoId}）");
            }
            if (input.Quantity <= 0)
            {
                throw new InvalidOperationException("购买数量必须大于0");
            }

            // 单价：优先用价格引擎算出的最终成交价，未命中策略则用 SKU 零售价
            var unitPrice = quotation.TryGetValue(input.SkuId, out var q) ? q.FinalPrice : sku.RetailPrice;

            items.Add(new TrxOrderItem
            {
                ProdInfoId = input.ProdInfoId,
                SkuId = input.SkuId,
                // ===== 以下均为下单瞬间的快照 =====
                ProdName = prod.ProdInfoName,
                SkuName = sku.SkuName ?? string.Empty,
                SpecValues = sku.SpecValues ?? string.Empty,
                // 图片优先取 SKU 图，没有则退回商品主图
                ProductImage = string.IsNullOrWhiteSpace(sku.Image) ? (prod.MainImage ?? string.Empty) : sku.Image,
                UnitPrice = unitPrice,                              // 单价 = 策略成交价 / SKU 零售价
                Quantity = input.Quantity,
                SubtotalAmount = unitPrice * input.Quantity         // 小计 = 单价 × 数量
            });
        }

        return items;
    }

    /// <summary>
    /// 按会员取价（与 ResolveQuotationAsync 同逻辑，但入参为提交明细列表）。
    /// 取价失败（会员不存在、无策略、参数异常等）返回空映射，下单回退标准售价。
    /// </summary>
    private async Task<Dictionary<long, QuotationResultDto>> ResolveQuotationByMemberAsync(
        long memberId, List<OrderSubmitItemDto> inputs)
    {
        var map = new Dictionary<long, QuotationResultDto>();
        if (memberId <= 0 || inputs.Count == 0)
        {
            return map;
        }

        try
        {
            var request = new QuotationRequestDto
            {
                MemMemberId = memberId,
                Items = inputs
                    .Select(i => new QuotationItemInputDto { MaterialId = i.SkuId, Quantity = i.Quantity })
                    .ToList()
            };

            var resp = await _priceService.QuotationAsync(request);
            if (resp.Code == 200 && resp.Data != null)
            {
                foreach (var r in resp.Data)
                {
                    map[r.MaterialId] = r;
                }
            }
        }
        catch
        {
            // 取价异常不阻断下单：回退标准售价
        }

        return map;
    }

    /// <summary>
    /// 校验明细并生成商品快照（提交专用，入参只含 SKU + 数量，prod_info_id 从 SKU 反查）。
    /// 校验项：SKU 存在、SKU 归属商品存在、数量 &gt; 0。
    /// 快照字段一律从 prod_info / prod_sku 实时读取；spec_json（镜片规格+光度）与 processing_status 透传落库。
    /// 不通过时抛 InvalidOperationException（中文消息），由调用方转 400/Fail。
    /// </summary>
    private async Task<List<TrxOrderItem>> BuildSubmitItemsAsync(
        List<OrderSubmitItemDto> inputs, Dictionary<long, QuotationResultDto> quotation)
    {
        var skuIds = inputs.Select(i => i.SkuId).Distinct().ToList();
        var skuMap = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var prodIds = skuMap.Values.Select(s => s.ProdInfoId).Distinct().ToList();
        var prodMap = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var items = new List<TrxOrderItem>();

        foreach (var input in inputs)
        {
            if (!skuMap.TryGetValue(input.SkuId, out var sku))
            {
                throw new InvalidOperationException($"SKU不存在（sku_id={input.SkuId}）");
            }
            if (!prodMap.TryGetValue(sku.ProdInfoId, out var prod))
            {
                throw new InvalidOperationException($"商品不存在（prod_info_id={sku.ProdInfoId}）");
            }
            if (input.Quantity <= 0)
            {
                throw new InvalidOperationException("购买数量必须大于0");
            }

            // 单价：命中价格策略取成交价，否则 SKU 零售价
            var unitPrice = quotation.TryGetValue(input.SkuId, out var q) ? q.FinalPrice : sku.RetailPrice;

            items.Add(new TrxOrderItem
            {
                ProdInfoId = sku.ProdInfoId,
                SkuId = input.SkuId,
                // ===== 下单瞬间快照 =====
                ProdName = prod.ProdInfoName,
                SkuName = sku.SkuName ?? string.Empty,
                SpecValues = sku.SpecValues ?? string.Empty,
                ProductImage = string.IsNullOrWhiteSpace(sku.Image) ? (prod.MainImage ?? string.Empty) : sku.Image,
                UnitPrice = unitPrice,
                Quantity = input.Quantity,
                SubtotalAmount = unitPrice * input.Quantity,
                // 镜片规格 + 光度 JSON 透传（确认页传入）
                SpecJson = input.SpecJson ?? string.Empty,
                // 加工状态：unprocessed-待加工 / processed-已加工
                ProcessingStatus = string.Equals(input.ProcessingStatus, "unprocessed", StringComparison.OrdinalIgnoreCase)
                    ? "unprocessed"
                    : "processed"
            });
        }

        return items;
    }

    /// <summary>
    /// 【销量累加】订单创建/支付事务内，按商品聚合累加 prod_info.sales_count。
    /// 设计要点：
    ///   1) 与订单写入共用同一事务——订单创建失败（回滚）时，销量自增一并回滚，不会脏加；
    ///   2) 使用 ExecuteUpdate 在数据库层做原子自增（COALESCE(sales_count,0)+qty），
    ///      避免并发竞态下的"读-改-写"丢失更新；
    ///   3) 同一订单只创建一次，因此本方法天然不会被重复调用，无需额外幂等标记；
    ///      支付回调（PayAsync / MarkPaidFromWriteOffAsync）不会重新进入下单事务，
    ///      故不会因重复回调导致销量重复累加。
    /// </summary>
    private async Task IncrementProductSalesAsync(List<TrxOrderItem> items)
    {
        if (items == null || items.Count == 0) return;

        // 同一商品可能拆成多个 SKU 明细，按商品(prod_info)聚合总数量
        var salesByProduct = items
            .GroupBy(i => i.ProdInfoId)
            .Select(g => new { ProdInfoId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .Where(g => g.Qty > 0)
            .ToList();

        foreach (var s in salesByProduct)
        {
            // 数据库级原子自增：UPDATE prod_info SET sales_count = COALESCE(sales_count,0) + @qty WHERE id = @id
            await _db.ProdInfos
                .Where(p => p.Id == s.ProdInfoId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.SalesCount, x => (x.SalesCount ?? 0) + s.Qty));
        }
    }

    /// <summary>
    /// 【阶段六】订单归属判定：会员令牌只能操作 BuyerId == 自己 的订单；管理员令牌（无 member_id）不受限。
    ///
    /// 用于支付 / 取消 / 确认收货这类"按 id 直接操作"的入口 —— 这些方法原先只按 id 取单，
    /// 会员改个 id 就能操作别人的订单（越权取消、越权支付）。
    /// 注意：本方法只做归属判断，不替代状态机校验。
    /// </summary>
    private bool CanAccessOrder(TrxOrder order)
        => !_currentUser.IsMember || order.BuyerId == _currentUser.MemberId;

    /// <summary>
    /// 【F-31 资损修复】订单金额主动校验：计算实付金额前先裁决参数合法性，非法直接抛业务异常。
    ///
    /// 背景：原实现为 `payAmount = total - discount + freight; if (payAmount &lt; 0) payAmount = 0;`，
    /// 属"先算完再被动归零"——前端把 discountAmount 放大到总额之上即可得到 0 元应付，
    /// 而订单仍会扣库存、置已支付、结清应收，造成 0 元拿货。
    ///
    /// 现在的规则（任一不满足即拒绝）：
    ///   1. 优惠金额不能为负；
    ///   2. 运费不能为负；
    ///   3. **优惠金额不得大于服务端重算的商品总价**（核心，杜绝 0 元单）；
    ///   4. 会员自助下单不允许非 0 优惠（改价必须走后台，需管理员权限）；
    ///   5. 会员自助下单实付金额必须 &gt; 0（后台代下单允许全额优惠的赠品单）。
    /// </summary>
    /// <param name="totalAmount">服务端价格引擎重算出的商品总价（权威值，不采信前端）</param>
    /// <param name="discount">前端传入的优惠金额</param>
    /// <param name="freight">前端传入的运费</param>
    /// <param name="forbidMemberDiscount">true=会员自助下单（禁止非 0 优惠）；false=后台代下单</param>
    /// <returns>校验通过的实付金额（已四舍五入到分）</returns>
    /// <exception cref="BusinessException">金额不合法时抛出，由 Controller 统一转成 ApiResponse.Fail</exception>
    private decimal ValidateOrderAmounts(
        decimal totalAmount, decimal discount, decimal freight, bool forbidMemberDiscount)
    {
        if (discount < 0)
        {
            throw new BusinessException("优惠金额不能为负数");
        }
        if (freight < 0)
        {
            throw new BusinessException("运费金额不能为负数");
        }

        // ★ 核心校验：优惠不得超过服务端重算的商品总价
        if (discount > totalAmount)
        {
            throw new BusinessException("优惠金额不能大于商品总价！");
        }

        // 会员自助下单不允许改价（否则 discount == total 仍可构造 0 元单）
        if (forbidMemberDiscount && discount > 0)
        {
            throw new BusinessException("会员自助下单不支持优惠金额，如需改价请联系客服");
        }

        var payAmount = decimal.Round(totalAmount - discount + freight, 2, MidpointRounding.AwayFromZero);

        // 理论上 discount ≤ total 已保证 payAmount ≥ 0，这里再兜一层防浮点/四舍五入异常
        if (payAmount < 0)
        {
            throw new BusinessException("应付金额不能为负数，请检查优惠金额与运费");
        }

        // 会员自助下单必须真实付款（>0）；后台代下单允许"全额优惠/赠品单"（payAmount=0）
        if (forbidMemberDiscount && payAmount <= 0)
        {
            throw new BusinessException("应付金额必须大于 0，请检查优惠金额与运费");
        }

        return payAmount;
    }

    /// <summary>
    /// 【乐观锁 CAS】扣减 SKU 库存并写变动日志。
    /// 采用比较并交换：UPDATE prod_sku SET stock=@after WHERE id=@id AND stock=@current，
    /// 受影响行数=0 说明被其他请求并发修改，重新读取最新库存后重试（最多 MaxCasRetry 次）。
    /// 之所以用 CAS 而非并发令牌：prod_sku 表没有 row_version 列，避免改动表结构。
    /// </summary>
    /// <param name="logs">变动日志收集器（事务提交前统一写入）</param>
    /// <param name="stockCache">本事务内的库存缓存，避免同一 SKU 多次扣减时读到旧值</param>
    /// <param name="delta">变动量，扣减时为负数</param>
    private async Task DeductStockAsync(
        List<LogStockLog> logs,
        Dictionary<long, int> stockCache,
        long prodInfoId, long skuId, int delta,
        string remark, long refId)
    {
        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            // 取当前库存：优先用本事务已更新的缓存值，否则从库实时读取
            int current;
            if (stockCache.TryGetValue(skuId, out var cached))
            {
                current = cached;
            }
            else
            {
                // 历史订单可能引用了已被删除的 SKU：此处必须给出明确业务提示，
                // 不能直接抛 LINQ 的 "Sequence contains no elements"。
                var stock = await _db.ProdSkus.AsNoTracking()
                    .Where(s => s.Id == skuId)
                    .Select(s => (int?)s.Stock)
                    .FirstOrDefaultAsync();
                if (stock == null)
                {
                    throw new InvalidOperationException(
                        $"商品SKU不存在或已被删除（sku_id={skuId}），无法扣减库存；请联系管理员处理该历史订单");
                }
                current = stock.Value;
            }

            var after = current + delta;

            // 库存不足：直接抛业务异常，由上层事务整体回滚
            if (after < 0)
            {
                throw new InvalidOperationException(
                    $"SKU(sku_id={skuId}) 库存不足（当前库存 {current}，本次需扣减 {-delta}）");
            }

            // 乐观锁：仅当库内 stock 仍等于读取到的 current 时才更新
            var affected = await _db.ProdSkus
                .Where(s => s.Id == skuId && s.Stock == current)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, after));

            if (affected == 1)
            {
                stockCache[skuId] = after;   // 更新缓存，供同 SKU 后续明细顺序扣减

                logs.Add(new LogStockLog
                {
                    ProdInfoId = prodInfoId,
                    SkuId = skuId,
                    ChangeType = ChangeTypeOrder,   // 4 = 订单扣减
                    ChangeQty = delta,
                    BeforeStock = current,
                    AfterStock = after,
                    RefType = RefTypeOrder,         // 'order'
                    RefId = refId,
                    Remark = remark,
                    Operator = SystemOperator
                });
                return;
            }
            // affected == 0：并发冲突，循环重读最新库存后重试
        }

        throw new InvalidOperationException($"SKU(sku_id={skuId}) 库存更新并发冲突，请重试");
    }

    /// <summary>
    /// 原子状态流转：仅当订单当前状态为 from 时更新为 to，返回是否抢到流转权。
    /// 必须在事务内调用：条件更新命中的行锁会保持到事务提交，
    /// 从而避免并发支付/关闭/发货/完成同时通过状态校验后相互覆盖。
    /// </summary>
    private async Task<bool> TryTransitionAsync(long orderId, int from, int to)
    {
        var affected = await _db.TrxOrders
            .Where(o => o.Id == orderId && o.OrderStatus == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, to));
        return affected == 1;
    }

    /// <summary>
    /// 生成订单号：ORD + yyyyMMddHHmmss + 4位随机数（如 ORD202609122310451234）。
    /// 极端情况下撞号（数据库 uk_order_no 唯一索引兜底）则重试，最多 5 次。
    /// </summary>
    private async Task<string> GenerateOrderNoAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var no = NoPrefix
                + DateTime.Now.ToString("yyyyMMddHHmmss")
                + Random.Shared.Next(0, 10000).ToString("D4");

            if (!await _orderRepository.OrderNoExistsAsync(no))
            {
                return no;
            }
        }

        throw new InvalidOperationException("订单号生成失败（号段冲突），请重试");
    }

    /// <summary>实体 → 列表 DTO（并填充状态/支付方式中文名）</summary>
    private static OrderListDto MapToListDto(TrxOrder o)
    {
        var dto = new OrderListDto();
        FillListDto(dto, o);
        return dto;
    }

    /// <summary>把主表实体字段填充到列表 DTO（列表与详情共用）</summary>
    private static void FillListDto(OrderListDto dto, TrxOrder o)
    {
        dto.Id = o.Id;
        dto.OrderNo = o.OrderNo;
        dto.BuyerId = o.BuyerId;

        dto.ReceiverName = o.ReceiverName;
        dto.ReceiverPhone = o.ReceiverPhone;
        dto.ReceiverProvince = o.ReceiverProvince;
        dto.ReceiverCity = o.ReceiverCity;
        dto.ReceiverDistrict = o.ReceiverDistrict;
        dto.ReceiverAddress = o.ReceiverAddress;

        dto.TotalAmount = o.TotalAmount;
        dto.DiscountAmount = o.DiscountAmount;
        dto.FreightAmount = o.FreightAmount;
        dto.PayAmount = o.PayAmount;

        dto.OrderStatus = o.OrderStatus;
        dto.OrderStatusName = OrderStatuses.GetName(o.OrderStatus);
        dto.PayMethod = o.PayMethod;
        dto.PayMethodName = PayMethods.GetName(o.PayMethod);
        dto.PayTime = o.PayTime;

        dto.ShipCompany = o.ShipCompany;
        dto.ShipNo = o.ShipNo;
        dto.ShippingImageUrl = o.ShippingImageUrl ?? string.Empty;

        dto.BuyerRemark = o.BuyerRemark;
        dto.MerchantRemark = o.MerchantRemark;
        dto.CloseReason = o.CloseReason;
        dto.CloseTime = o.CloseTime;
        dto.CompleteTime = o.CompleteTime;

        dto.CreateTime = o.CreateTime;
        dto.UpdateTime = o.UpdateTime;
    }

    /// <summary>明细实体 → DTO</summary>
    private static OrderItemDto MapToItemDto(TrxOrderItem i) => new()
    {
        Id = i.Id,
        OrderId = i.OrderId,
        ProdInfoId = i.ProdInfoId,
        SkuId = i.SkuId,
        ProdName = i.ProdName,
        SkuName = i.SkuName,
        SpecValues = i.SpecValues,
        ProductImage = i.ProductImage,
        UnitPrice = i.UnitPrice,
        Quantity = i.Quantity,
        SubtotalAmount = i.SubtotalAmount,
        CreateTime = i.CreateTime
    };

    // ============================================================
    // 客户统计联动（与 mem_member 的耦合点）
    // 说明：订单支付/完成时回写会员累计消费/订单数/最近订单，
    //       使"客户列表的累计消费/最近订单"字段始终与订单保持一致。
    //       会员不存在（如历史脏数据、buyer_id 非会员）时静默跳过，不影响订单主流程。
    // ============================================================

    /// <summary>
    /// 订单支付成功后的会员统计回写：累计消费 + 实付金额、累计订单数 +1、最近订单号/时间刷新。
    /// 注意：累计消费与订单数只在"支付"时计入，避免与"完成"重复统计。
    /// </summary>
    private async Task UpdateMemberOnPaidAsync(long buyerId, decimal payAmount, string orderNo)
    {
        // 【阶段八】改为数据库侧原子自增，不再"查实体 → C# 加 → SaveChanges"：
        //   1) 同一会员并发支付多单时，读改写会丢更新（两次 +10 只记 10）；
        //   2) 这里刻意不加载实体，避免把余额等字段带进 UPDATE（余额由实时扣减维护）。
        await _db.MemMembers
            .Where(m => m.Id == buyerId && m.IsDeleted == 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.TotalConsumption, m => m.TotalConsumption + payAmount)
                .SetProperty(m => m.OrderCount, m => m.OrderCount + 1)
                .SetProperty(m => m.LastOrderNo, orderNo)
                .SetProperty(m => m.LastOrderTime, DateTime.Now));
    }

    /// <summary>
    /// 订单完成后的会员统计回写：已完成订单数 +1（累计消费/订单数已在支付时计入）。
    /// </summary>
    private async Task UpdateMemberOnCompletedAsync(long buyerId)
    {
        await _db.MemMembers
            .Where(m => m.Id == buyerId && m.IsDeleted == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(
                m => m.CompletedOrderCount, m => m.CompletedOrderCount + 1));
    }
}
