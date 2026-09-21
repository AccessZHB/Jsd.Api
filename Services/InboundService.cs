using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 采购入库（以采定入）服务实现。
///
/// 【两阶段设计】
///   创建(Create)：仅校验 + 写"待确认"(0) 入库单与明细，不碰库存/应付；
///   确认(Confirm)：在单个事务内 ——
///     1) 重做超量拦截（针对其他已确认入库单）；
///     2) 增加 prod_sku.stock（库存以 SKU 为最小单位）；
///     3) 写 log_stock_log（change_type=1 入库，ref_type=pur_inbound）；
///     4) 自动生成 pur_payable（应付总额 = Σ 实收数量 × 订单明细单价）；
///     5) 推进采购订单状态：全部明细收满 → 4-已结清；否则 → 3-部分入库。
///
/// 【库存一致性】库存只落在 prod_sku.stock；同一事务内同一 SKU 用缓存字典保证
/// before/after 顺序累计，与 StockInService 同一套写法。
/// </summary>
public class InboundService : IInboundService
{
    /// <summary>库存变动类型：入库（对应 log_stock_log.change_type=1）</summary>
    private const int ChangeTypeIn = 1;

    private readonly AppDbContext _db;
    private readonly IRepository<ProdSku> _skuRepository;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly InboundCreateValidator _createValidator;

    public InboundService(
        AppDbContext db,
        IRepository<ProdSku> skuRepository,
        IMapper mapper,
        CurrentUserService currentUser,
        InboundCreateValidator createValidator)
    {
        _db = db;
        _skuRepository = skuRepository;
        _mapper = mapper;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    // ============================================================
    // 查询
    // ============================================================

    public async Task<ApiResponse<PagedResult<InboundListDto>>> GetPagedListAsync(InboundQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 || query.PageSize > 100 ? 20 : query.PageSize;

        var q = _db.PurInbounds.AsNoTracking();

        if (query.SupplierId.HasValue)
        {
            // supplier 在订单上，经 Order 关联筛选（EF 会自动 JOIN）
            q = q.Where(i => i.Order != null && i.Order.SupplierId == query.SupplierId.Value);
        }

        if (query.OrderId.HasValue)
        {
            q = q.Where(i => i.OrderId == query.OrderId.Value);
        }

        if (query.WarehouseId.HasValue)
        {
            q = q.Where(i => i.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(i => i.Status == query.Status.Value);
        }

        if (query.InboundDateStart.HasValue)
        {
            q = q.Where(i => i.InboundDate >= query.InboundDateStart.Value);
        }

        if (query.InboundDateEnd.HasValue)
        {
            q = q.Where(i => i.InboundDate <= query.InboundDateEnd.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(i => i.InboundNo.Contains(kw)
                              || (i.Order != null && i.Order.OrderNo.Contains(kw)));
        }

        var total = await q.CountAsync();
#pragma warning disable CS8602
        var rows = await q.Include(i => i.Order).ThenInclude(o => o.Supplier)
            .OrderByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
#pragma warning restore CS8602

        var list = rows.Select(MapListDto).ToList();
        return ApiResponse<PagedResult<InboundListDto>>.Success(
            PagedResult<InboundListDto>.Create(list, total, page, pageSize));
    }

    public async Task<ApiResponse<InboundDetailDto>> GetDetailAsync(long id)
    {
        var inbound = await _db.PurInbounds.AsNoTracking()
#pragma warning disable CS8602
            .Include(i => i.Order).ThenInclude(o => o.Supplier)
#pragma warning restore CS8602
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inbound == null)
        {
            return ApiResponse<InboundDetailDto>.Fail("入库单不存在", 404);
        }

        var dto = new InboundDetailDto
        {
            Id = inbound.Id,
            InboundNo = inbound.InboundNo,
            OrderId = inbound.OrderId,
            OrderNo = inbound.Order?.OrderNo,
            SupplierId = inbound.Order?.SupplierId ?? 0,
            SupplierName = inbound.Order?.Supplier?.SupplierName,
            WarehouseId = inbound.WarehouseId,
            InboundDate = inbound.InboundDate,
            OperatorId = inbound.OperatorId,
            Status = inbound.Status,
            StatusText = InboundStatusHelper.GetName(inbound.Status),
            Remark = inbound.Remark,
            CreateTime = inbound.CreateTime,
            UpdateTime = inbound.UpdateTime
        };

        // 回填明细的物料名称与采购单价（单价来自订单明细，便于核对）
        var orderItemIds = inbound.Items.Select(it => it.OrderItemId).Distinct().ToList();
        var orderItems = await _db.PurOrderItems.AsNoTracking()
            .Where(oi => orderItemIds.Contains(oi.Id))
            .ToListAsync();

        var skuIds = orderItems.Select(oi => oi.MaterialId).Distinct().ToList();
        var skuMap = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SkuName);

        foreach (var it in inbound.Items)
        {
            var oi = orderItems.FirstOrDefault(x => x.Id == it.OrderItemId);
            dto.Items.Add(new InboundDetailItemDto
            {
                Id = it.Id,
                OrderItemId = it.OrderItemId,
                MaterialId = it.MaterialId,
                MaterialName = skuMap.TryGetValue(it.MaterialId, out var name) ? name : null,
                UnitPrice = oi?.UnitPrice ?? 0,
                ActualQuantity = it.ActualQuantity,
                BatchNo = it.BatchNo,
                Remark = it.Remark
            });
        }

        return ApiResponse<InboundDetailDto>.Success(dto);
    }

    public async Task<ApiResponse<List<InboundListDto>>> GetByOrderAsync(long orderId)
    {
        var rows = await _db.PurInbounds.AsNoTracking()
#pragma warning disable CS8602
            .Include(i => i.Order).ThenInclude(o => o.Supplier)
#pragma warning restore CS8602
            .Where(i => i.OrderId == orderId)
            .OrderByDescending(i => i.Id)
            .ToListAsync();

        return ApiResponse<List<InboundListDto>>.Success(rows.Select(MapListDto).ToList());
    }

    // ============================================================
    // 创建（以采定入）
    // ============================================================

    public async Task<ApiResponse<object>> CreateAsync(CreateInboundDto dto)
    {
        _createValidator.ValidateAndThrow(dto);

        // 采购订单必须存在且为"审批通过/待入库"
        var order = await _db.PurOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId);
        if (order == null)
        {
            throw new InvalidOperationException("采购订单不存在");
        }

        // 允许 审批通过(2) 与 部分入库(3)：
        // 分批到货场景下，第一张入库单确认后订单会被推进为"部分入库(3)"，
        // 后续还要继续开单收剩下的货，因此这里必须同时放行 2 和 3，否则分批入库会卡死。
        if (order.Status != (int)PurchaseOrderStatus.Approved
            && order.Status != (int)PurchaseOrderStatus.PartialIn)
        {
            throw new InvalidOperationException("仅审批通过/部分入库的采购订单可入库");
        }

        // 入库明细行必须属于该订单
        var orderItemIds = order.Items.Select(oi => oi.Id).ToHashSet();
        foreach (var it in dto.Items)
        {
            if (!orderItemIds.Contains(it.OrderItemId))
            {
                throw new InvalidOperationException($"入库明细行 {it.OrderItemId} 不属于该采购订单");
            }
        }

        // 超量拦截：已确认入库总数 + 本次 ≤ 订单数量（本次待确认单不计入已确认）
        var confirmedByItem = await GetConfirmedReceivedByItemAsync(dto.OrderId, null);
        foreach (var it in dto.Items)
        {
            var oi = order.Items.First(oi => oi.Id == it.OrderItemId);
            var received = confirmedByItem.TryGetValue(it.OrderItemId, out var r) ? r : 0;
            if (received + it.ActualQuantity > oi.Quantity)
            {
                throw new InvalidOperationException(
                    $"物料(明细行{it.OrderItemId})超出未入库数量：已入{received}，本次{it.ActualQuantity}，订单{oi.Quantity}");
            }
        }

        var inboundNo = await PurchaseNumberHelper.GenerateAsync(
            _db, "RK", no => _db.PurInbounds.AnyAsync(i => i.InboundNo == no));

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var inbound = new PurInbound
            {
                InboundNo = inboundNo,
                OrderId = dto.OrderId,
                WarehouseId = dto.WarehouseId,
                InboundDate = dto.InboundDate.Date,
                OperatorId = dto.OperatorId ?? _currentUser.UserId,
                Status = (int)InboundStatus.Pending,
                Remark = dto.Remark
            };
            await _db.PurInbounds.AddAsync(inbound);
            await _db.SaveChangesAsync();   // 拿自增主键

            foreach (var it in dto.Items)
            {
                var oi = order.Items.First(oi => oi.Id == it.OrderItemId);
                _db.PurInboundItems.Add(new PurInboundItem
                {
                    InboundId = inbound.Id,
                    OrderItemId = it.OrderItemId,
                    MaterialId = oi.MaterialId,
                    ActualQuantity = it.ActualQuantity,
                    BatchNo = it.BatchNo,
                    Remark = it.Remark
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ApiResponse<object>.Success(
                new { id = inbound.Id, inboundNo = inbound.InboundNo }, "入库单已创建，待确认");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 确认入库（核心事务）
    // ============================================================

    public async Task<ApiResponse<object>> ConfirmAsync(long id)
    {
        var inbound = await _db.PurInbounds
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inbound == null)
        {
            return ApiResponse<object>.Fail("入库单不存在", 404);
        }

        if (inbound.Status != (int)InboundStatus.Pending)
        {
            return ApiResponse<object>.Fail("仅待确认的入库单可确认");
        }

        var order = await _db.PurOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == inbound.OrderId);
        if (order == null)
        {
            throw new InvalidOperationException("关联的采购订单不存在");
        }

        if (order.Status != (int)PurchaseOrderStatus.Approved
            && order.Status != (int)PurchaseOrderStatus.PartialIn)
        {
            throw new InvalidOperationException("采购订单状态异常，无法确认入库");
        }

        // 重做超量拦截（针对其他已确认入库单，排除本单）
        var confirmedByItem = await GetConfirmedReceivedByItemAsync(inbound.OrderId, inbound.Id);
        foreach (var it in inbound.Items)
        {
            var oi = order.Items.FirstOrDefault(oi => oi.Id == it.OrderItemId);
            if (oi == null)
            {
                throw new InvalidOperationException($"入库明细行 {it.OrderItemId} 对应的采购订单明细不存在");
            }

            var received = confirmedByItem.TryGetValue(it.OrderItemId, out var r) ? r : 0;
            if (received + it.ActualQuantity > oi.Quantity)
            {
                throw new InvalidOperationException(
                    $"物料(明细行{it.OrderItemId})超出未入库数量，无法确认：已入{received}，本次{it.ActualQuantity}，订单{oi.Quantity}");
            }
        }

        var opUser = !string.IsNullOrEmpty(_currentUser.UserName)
            ? _currentUser.UserName
            : (inbound.OperatorId?.ToString() ?? "系统");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1) 增加库存 + 写变动日志；同一 SKU 用缓存保证顺序累计
            var skuCache = new Dictionary<long, ProdSku>();
            var logList = new List<LogStockLog>();
            var payableTotal = 0m;

            // 一次性取出本单关联 SKU 的 prod_info_id（变动日志需要）
            var skuIdList = inbound.Items.Select(it => it.MaterialId).Distinct().ToList();
            var skuInfoMap = await _db.ProdSkus.AsNoTracking()
                .Where(s => skuIdList.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.ProdInfoId);

            foreach (var it in inbound.Items)
            {
                var oi = order.Items.First(oi => oi.Id == it.OrderItemId);
                var sku = await GetTrackedSkuAsync(it.MaterialId, skuCache);

                var beforeStock = sku.Stock;
                sku.Stock = beforeStock + (int)Math.Round(it.ActualQuantity, 0, MidpointRounding.AwayFromZero);

                logList.Add(new LogStockLog
                {
                    ProdInfoId = skuInfoMap.TryGetValue(it.MaterialId, out var pid) ? pid : 0,
                    SkuId = it.MaterialId,
                    ChangeType = ChangeTypeIn,
                    ChangeQty = (int)Math.Round(it.ActualQuantity, 0, MidpointRounding.AwayFromZero),
                    BeforeStock = beforeStock,
                    AfterStock = sku.Stock,
                    RefType = "pur_inbound",
                    RefId = inbound.Id,
                    Remark = $"采购入库 {inbound.InboundNo}",
                    Operator = opUser
                });

                payableTotal = decimal.Round(payableTotal + it.ActualQuantity * oi.UnitPrice, 2, MidpointRounding.AwayFromZero);
            }

            // 2) 自动生成应付账款
            if (payableTotal > 0)
            {
                var payableNo = await PurchaseNumberHelper.GenerateAsync(
                    _db, "AP", no => _db.PurPayables.AnyAsync(p => p.PayableNo == no));

                _db.PurPayables.Add(new PurPayable
                {
                    PayableNo = payableNo,
                    SupplierId = order.SupplierId,
                    RelatedOrderId = order.Id,
                    TotalAmount = payableTotal,
                    PaidAmount = 0,
                    UnpaidAmount = payableTotal,
                    DueDate = null,
                    Status = (int)PayableStatus.Unpaid,
                    Remark = $"采购入库 {inbound.InboundNo} 自动生成"
                });
            }

            // 3) 推进采购订单状态：全部明细收满 → 已结清(4)，否则 → 部分入库(3)
            var newReceived = await GetConfirmedReceivedByItemAsync(inbound.OrderId, null);
            foreach (var it in inbound.Items)
            {
                if (newReceived.ContainsKey(it.OrderItemId))
                {
                    newReceived[it.OrderItemId] += it.ActualQuantity;
                }
                else
                {
                    newReceived[it.OrderItemId] = it.ActualQuantity;
                }
            }

            var allDone = order.Items.All(oi =>
                newReceived.TryGetValue(oi.Id, out var r) && r >= oi.Quantity);
            order.Status = allDone
                ? (int)PurchaseOrderStatus.Completed
                : (int)PurchaseOrderStatus.PartialIn;

            // 4) 入库单置为已入库
            inbound.Status = (int)InboundStatus.Confirmed;

            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var msg = allDone ? "入库确认完成，采购订单已结清" : "入库确认完成，采购订单部分入库";
            return ApiResponse<object>.Success(new { id = inbound.Id, inboundNo = inbound.InboundNo }, msg);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    /// <summary>
    /// 统计某采购订单下，各明细行"已确认入库单"的已收数量。
    /// excludeInboundId 非空时排除该入库单（用于确认前重算"其他已确认单"的已收）。
    /// </summary>
    private async Task<Dictionary<long, decimal>> GetConfirmedReceivedByItemAsync(
        long orderId, long? excludeInboundId)
    {
            var q = from bi in _db.PurInboundItems
                join ib in _db.PurInbounds on bi.InboundId equals ib.Id
                where ib.OrderId == orderId
                      && ib.Status == (int)InboundStatus.Confirmed
                      && (excludeInboundId == null || bi.InboundId != excludeInboundId.Value)
                select new { OrderItemId = bi.OrderItemId, bi.ActualQuantity };

        var rows = await q.ToListAsync();
        return rows.GroupBy(x => x.OrderItemId)
                   .ToDictionary(g => g.Key, g => g.Sum(x => x.ActualQuantity));
    }

    /// <summary>获取被 ChangeTracker 跟踪的 SKU 实体（与 StockInService 同款写法）</summary>
    private async Task<ProdSku> GetTrackedSkuAsync(long skuId, Dictionary<long, ProdSku> cache)
    {
        if (cache.TryGetValue(skuId, out var cached))
        {
            return cached;
        }

        var sku = await _db.ProdSkus.FirstOrDefaultAsync(s => s.Id == skuId)
                 ?? throw new InvalidOperationException($"SKU不存在（sku_id={skuId}）");

        cache[skuId] = sku;
        return sku;
    }

    /// <summary>实体 → 列表 DTO（供应商名经订单联查）</summary>
    private static InboundListDto MapListDto(PurInbound i) => new()
    {
        Id = i.Id,
        InboundNo = i.InboundNo,
        SupplierId = i.Order?.SupplierId ?? 0,
        SupplierName = i.Order?.Supplier?.SupplierName,
        OrderId = i.OrderId,
        OrderNo = i.Order?.OrderNo,
        WarehouseId = i.WarehouseId,
        InboundDate = i.InboundDate,
        OperatorId = i.OperatorId,
        Status = i.Status,
        StatusText = InboundStatusHelper.GetName(i.Status),
        Remark = i.Remark,
        CreateTime = i.CreateTime
    };
}
