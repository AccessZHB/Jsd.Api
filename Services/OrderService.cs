using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

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

    public OrderService(
        AppDbContext db,
        IOrderRepository orderRepository,
        IOrderItemRepository itemRepository,
        IRepository<MemMember> memberRepository,
        IReceivableService receivableService)
    {
        _db = db;
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _memberRepository = memberRepository;
        _receivableService = receivableService;
    }

    // ============================================================
    // 1. 分页查询订单列表
    // ============================================================

    /// <summary>
    /// 分页查询订单（订单号 / 状态 / 创建时间范围 筛选），返回主表信息 + 商品总数。
    /// 商品总数用一次 group by 统计当前页所有订单，避免逐单查询产生 N+1。
    /// </summary>
    public async Task<ApiResponse<PagedResult<OrderListDto>>> GetPagedListAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, total) = await _orderRepository.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, page, pageSize);

        var list = orders.Select(MapToListDto).ToList();

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

        var dto = new OrderDetailDto();
        FillListDto(dto, order);
        dto.ItemCount = order.Items.Count;
        dto.TotalQuantity = order.Items.Sum(i => i.Quantity);
        dto.Items = order.Items.Select(MapToItemDto).ToList();

        return ApiResponse<OrderDetailDto>.Success(dto);
    }

    // ============================================================
    // 3. 创建订单（生成订单号 + 商品快照 + 金额重算 + 事务）
    // ============================================================

    /// <summary>
    /// 创建订单：
    /// 1) 校验商品/SKU 存在性与归属，数量必须 &gt; 0；
    /// 2) 生成不重复订单号（ORD + yyyyMMddHHmmss + 4位随机数）；
    /// 3) 从 prod_info / prod_sku 读取名称、规格、图片、零售价写入明细【快照】；
    /// 4) 后端重算金额（不信任前端）；
    /// 5) 事务写入主表 + 明细，初始状态 = 待付款 Pending(0)。
    /// 注意：创建阶段【不扣库存】，库存扣减发生在支付时。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(OrderCreateDto dto)
    {
        // 1) 校验 + 生成明细快照（不通过抛 InvalidOperationException，由 Controller 转 400）
        var items = await BuildItemSnapshotsAsync(dto.Items);

        // 2) 生成订单号
        var orderNo = await GenerateOrderNoAsync();

        // 3) 后端重算金额
        var totalAmount = items.Sum(i => i.SubtotalAmount);
        var payAmount = totalAmount - dto.DiscountAmount + dto.FreightAmount;
        if (payAmount < 0) payAmount = 0;   // 优惠大于总额时归零，避免出现负金额

        // 4) 事务写入
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

            await tx.CommitAsync();

            return ApiResponse<object>.Success(
                new { id = order.Id, orderNo = order.OrderNo, payAmount = order.PayAmount },
                "订单创建成功，待付款");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
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

            // 【联动客户统计】订单支付即视为"有效订单"：累计消费 + 订单数 + 最近订单回写。
            // buyer_id 即 mem_member.id；客户不存在（如历史脏数据）时跳过，不影响主流程。
            await UpdateMemberOnPaidAsync(order.BuyerId, order.PayAmount, order.OrderNo);

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
            order.ShipCompany = dto.ShipCompany;
            order.ShipNo = dto.ShipNo;
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
                new { id = id, orderNo = order.OrderNo, shipCompany = order.ShipCompany, shipNo = order.ShipNo },
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

        // 【状态校验】只有已发货才能确认完成
        if (order.OrderStatus != OrderStatuses.Shipped)
        {
            return ApiResponse<object>.Fail("仅已发货订单可确认完成（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        var items = await _itemRepository.GetByOrderIdAsync(id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
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
    /// </summary>
    public async Task<ApiResponse<object>> CancelAsync(long id, OrderCloseDto dto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || order.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("订单不存在", 404);
        }

        if (order.OrderStatus != OrderStatuses.Pending)
        {
            return ApiResponse<object>.Fail("仅待付款订单可关闭（当前状态：" + OrderStatuses.GetName(order.OrderStatus) + "）");
        }

        order.OrderStatus = OrderStatuses.Closed;
        order.CloseReason = dto.CloseReason ?? string.Empty;
        order.CloseTime = DateTime.Now;

        await _orderRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id, orderNo = order.OrderNo }, "订单已关闭");
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
    // 9. 订单导出（占位实现）
    // ============================================================

    /// <summary>
    /// 订单导出【占位实现】。
    /// 复用与列表完全相同的筛选条件统计命中条数，暂不生成真实 Excel：
    /// 后续接入 EPPlus / MiniExcel 后，只需在此生成文件并把地址赋给 ExportUrl，
    /// 接口签名与前端调用方式均无需变动。
    /// </summary>
    public async Task<ApiResponse<OrderExportDto>> ExportAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime)
    {
        // 复用列表仓储的筛选逻辑，pageSize=1 只为拿 total
        var (_, total) = await _orderRepository.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, 1, 1);

        var dto = new OrderExportDto
        {
            ExportUrl = null,   // 占位：真实实现后填入文件下载地址
            Message = "订单导出功能开发中，敬请期待（当前为占位接口）",
            Total = total
        };

        return ApiResponse<OrderExportDto>.Success(dto);
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    /// <summary>
    /// 校验明细并生成商品快照。
    /// 校验项：商品存在、SKU 存在、SKU 归属该商品、数量 &gt; 0。
    /// 快照字段（prod_name / sku_name / spec_values / product_image / unit_price）
    /// 一律从 prod_info、prod_sku 实时读取写入，保证商品后续改价改名不影响历史订单。
    /// 不通过时抛 InvalidOperationException（中文消息），由 Controller 统一转 400。
    /// </summary>
    private async Task<List<TrxOrderItem>> BuildItemSnapshotsAsync(List<OrderItemInputDto> inputs)
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
                UnitPrice = sku.RetailPrice,                       // 单价 = SKU 当前零售价
                Quantity = input.Quantity,
                SubtotalAmount = sku.RetailPrice * input.Quantity   // 小计 = 单价 × 数量
            });
        }

        return items;
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
            var current = stockCache.TryGetValue(skuId, out var cached)
                ? cached
                : await _db.ProdSkus.AsNoTracking()
                    .Where(s => s.Id == skuId)
                    .Select(s => s.Stock)
                    .FirstAsync();

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
        var member = await _db.MemMembers
            .FirstOrDefaultAsync(m => m.Id == buyerId && m.IsDeleted == 0);
        if (member == null) return;   // 客户不存在则跳过联动

        member.TotalConsumption += payAmount;
        member.OrderCount += 1;
        member.LastOrderNo = orderNo;
        member.LastOrderTime = DateTime.Now;
    }

    /// <summary>
    /// 订单完成后的会员统计回写：已完成订单数 +1（累计消费/订单数已在支付时计入）。
    /// </summary>
    private async Task UpdateMemberOnCompletedAsync(long buyerId)
    {
        var member = await _db.MemMembers
            .FirstOrDefaultAsync(m => m.Id == buyerId && m.IsDeleted == 0);
        if (member == null) return;

        member.CompletedOrderCount += 1;
    }
}
