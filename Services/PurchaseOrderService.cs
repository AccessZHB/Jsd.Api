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
/// 采购订单服务实现。
///
/// 【状态机 · 无审核环节】
///   0-草稿 ──确认订单──&gt; 2-待入库 ──入库确认──&gt; 3-部分入库 / 4-已结清；
///   0 ──作废──&gt; 9-作废。
/// 说明：本系统采购【不做审批】，因此 pur_order 不设 approver_id / approve_time，
///       也不提供审批接口；"确认订单"仅把草稿推进为可入库状态，不记录审批人。
///       枚举中的 1-审批中 仅为历史数据兼容保留，业务流程不再产生该状态。
///
/// 【事务】创建涉及主表+明细+重算总额，统一用 DbContext 事务包裹。
/// 【软删除】作废仅置 status=9，不物理删除（规范 6.7）。
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _db;
    private readonly IRepository<SupSupplier> _supplierRepository;
    private readonly IRepository<ProdSku> _skuRepository;
    private readonly IRepository<ProdInfo> _prodInfoRepository;
    private readonly IRepository<LogStockWarning> _warningRepository;
    private readonly IMapper _mapper;
    private readonly PurchaseOrderCreateValidator _createValidator;

    public PurchaseOrderService(
        AppDbContext db,
        IRepository<SupSupplier> supplierRepository,
        IRepository<ProdSku> skuRepository,
        IRepository<ProdInfo> prodInfoRepository,
        IRepository<LogStockWarning> warningRepository,
        IMapper mapper,
        PurchaseOrderCreateValidator createValidator)
    {
        _db = db;
        _supplierRepository = supplierRepository;
        _skuRepository = skuRepository;
        _prodInfoRepository = prodInfoRepository;
        _warningRepository = warningRepository;
        _mapper = mapper;
        _createValidator = createValidator;
    }

    // ============================================================
    // 列表 / 详情
    // ============================================================

    public async Task<ApiResponse<PagedResult<PurchaseOrderListDto>>> GetPagedListAsync(PurchaseOrderQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 || query.PageSize > 100 ? 20 : query.PageSize;

        var q = _db.PurOrders.AsNoTracking();

        if (query.SupplierId.HasValue)
        {
            q = q.Where(o => o.SupplierId == query.SupplierId.Value);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(o => o.Status == query.Status.Value);
        }

        if (query.OrderDateStart.HasValue)
        {
            q = q.Where(o => o.OrderDate >= query.OrderDateStart.Value);
        }

        if (query.OrderDateEnd.HasValue)
        {
            q = q.Where(o => o.OrderDate <= query.OrderDateEnd.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(o => o.OrderNo.Contains(kw)
                              || (o.Supplier != null && o.Supplier.SupplierName.Contains(kw)));
        }

        var total = await q.CountAsync();
        var rows = await q
            .Include(o => o.Supplier)
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = rows.Select(MapListDto).ToList();
        return ApiResponse<PagedResult<PurchaseOrderListDto>>.Success(
            PagedResult<PurchaseOrderListDto>.Create(list, total, page, pageSize));
    }

    public async Task<ApiResponse<PurchaseOrderDetailDto>> GetDetailAsync(long id)
    {
        var order = await _db.PurOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return ApiResponse<PurchaseOrderDetailDto>.Fail("采购订单不存在", 404);
        }

        var dto = new PurchaseOrderDetailDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.SupplierName,
            OrderDate = order.OrderDate,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            Status = order.Status,
            StatusText = PurchaseOrderStatusHelper.GetName(order.Status),
            TotalAmount = order.TotalAmount,
            Remark = order.Remark,
            CreateTime = order.CreateTime,
            UpdateTime = order.UpdateTime
        };

        // 各明细"已入库数量" = 已确认入库单中对应行的实收之和
        var receivedByItem = await GetConfirmedReceivedAsync();

        // 批量回填物料展示信息：SKU 名称 / 规格 / 所属商品ID / 单位
        // （前端编辑与入库单都需要 prod_info_id 才能在"商品→SKU"级联下拉里正确回显）
        var skuIds = order.Items.Select(i => i.MaterialId).Distinct().ToList();
        var skuInfos = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SkuName, s.SpecValues, s.ProdInfoId })
            .ToListAsync();
        var skuMap = skuInfos.ToDictionary(s => s.Id, s => s);

        var prodIds = skuInfos.Select(s => s.ProdInfoId).Distinct().ToList();
        var unitMap = await _db.ProdInfos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Unit);

        foreach (var item in order.Items)
        {
            skuMap.TryGetValue(item.MaterialId, out var sku);
            var prodInfoId = sku?.ProdInfoId;

            dto.Items.Add(new PurchaseOrderDetailItemDto
            {
                Id = item.Id,
                MaterialId = item.MaterialId,
                MaterialName = sku?.SkuName,
                ProdInfoId = prodInfoId,
                SpecValues = sku?.SpecValues,
                Unit = prodInfoId.HasValue && unitMap.TryGetValue(prodInfoId.Value, out var u) ? u : null,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal,
                ReceivedQuantity = receivedByItem.TryGetValue(item.Id, out var r) ? r : 0,
                Remark = item.Remark
            });
        }

        dto.ReceivedTotal = dto.Items.Sum(i => i.ReceivedQuantity);
        dto.UnreceivedTotal = decimal.Round(
            dto.Items.Sum(i => i.Quantity - i.ReceivedQuantity), 2, MidpointRounding.AwayFromZero);

        return ApiResponse<PurchaseOrderDetailDto>.Success(dto);
    }

    /// <summary>统计"已确认入库单"中各采购订单明细行的已收数量</summary>
    private async Task<Dictionary<long, decimal>> GetConfirmedReceivedAsync()
    {
        var confirmed = await (
            from bi in _db.PurInboundItems
            join ib in _db.PurInbounds on bi.InboundId equals ib.Id
            where ib.Status == (int)InboundStatus.Confirmed
            select new { OrderItemId = bi.OrderItemId, bi.ActualQuantity }
        ).ToListAsync();

        return confirmed
            .GroupBy(x => x.OrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ActualQuantity));
    }

    public async Task<ApiResponse<List<PurchaseOrderListDto>>> GetBySupplierAsync(long supplierId, int? status)
    {
        var q = _db.PurOrders.AsNoTracking().Include(o => o.Supplier)
            .Where(o => o.SupplierId == supplierId);

        if (status.HasValue)
        {
            q = q.Where(o => o.Status == status.Value);
        }

        var rows = await q.OrderByDescending(o => o.Id).ToListAsync();
        return ApiResponse<List<PurchaseOrderListDto>>.Success(rows.Select(MapListDto).ToList());
    }

    // ============================================================
    // 创建
    // ============================================================

    public async Task<ApiResponse<PurchaseOrderCreateResult>> CreateAsync(CreatePurchaseOrderDto dto)
    {
        _createValidator.ValidateAndThrow(dto);

        // 供应商必须存在且启用
        var supplier = await _supplierRepository.GetByIdAsync(dto.SupplierId);
        if (supplier == null)
        {
            throw new InvalidOperationException("供应商不存在");
        }

        if (supplier.Status != 1)
        {
            throw new InvalidOperationException("供应商已禁用，无法创建采购订单");
        }

        // 物料（SKU）必须存在
        await ValidateMaterialsAsync(dto.Items);

        var orderNo = await PurchaseNumberHelper.GenerateAsync(
            _db, "PO", no => _db.PurOrders.AnyAsync(o => o.OrderNo == no));

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new PurOrder
            {
                OrderNo = orderNo,
                SupplierId = dto.SupplierId,
                OrderDate = dto.OrderDate.Date,
                ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
                Status = (int)PurchaseOrderStatus.Draft,
                TotalAmount = 0,
                Remark = dto.Remark
            };
            await _db.PurOrders.AddAsync(order);
            await _db.SaveChangesAsync();   // 先拿自增主键

            var total = 0m;
            foreach (var it in dto.Items)
            {
                var subtotal = decimal.Round(it.Quantity * it.UnitPrice, 2, MidpointRounding.AwayFromZero);
                total += subtotal;

                _db.PurOrderItems.Add(new PurOrderItem
                {
                    OrderId = order.Id,
                    MaterialId = it.MaterialId,
                    Quantity = it.Quantity,
                    UnitPrice = decimal.Round(it.UnitPrice, 2, MidpointRounding.AwayFromZero),
                    Subtotal = subtotal,
                    Remark = it.Remark
                });
            }

            order.TotalAmount = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return ApiResponse<PurchaseOrderCreateResult>.Success(
                new PurchaseOrderCreateResult { Id = order.Id, OrderNo = order.OrderNo }, "采购订单已创建");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 更新（仅草稿）
    // ============================================================

    public async Task<ApiResponse<object>> UpdateAsync(long id, UpdatePurchaseOrderDto dto)
    {
        _createValidator.ValidateAndThrow(new CreatePurchaseOrderDto
        {
            SupplierId = dto.SupplierId,
            OrderDate = dto.OrderDate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Remark = dto.Remark,
            Items = dto.Items
        });

        var order = await _db.PurOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("采购订单不存在", 404);
        }

        if (order.Status != (int)PurchaseOrderStatus.Draft)
        {
            return ApiResponse<object>.Fail("仅草稿状态的采购订单可编辑");
        }

        await ValidateMaterialsAsync(dto.Items);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 删除旧明细，写入新明细
            var oldItems = await _db.PurOrderItems.Where(i => i.OrderId == id).ToListAsync();
            if (oldItems.Count > 0)
            {
                _db.PurOrderItems.RemoveRange(oldItems);
            }

            var total = 0m;
            foreach (var it in dto.Items)
            {
                var subtotal = decimal.Round(it.Quantity * it.UnitPrice, 2, MidpointRounding.AwayFromZero);
                total += subtotal;

                _db.PurOrderItems.Add(new PurOrderItem
                {
                    OrderId = id,
                    MaterialId = it.MaterialId,
                    Quantity = it.Quantity,
                    UnitPrice = decimal.Round(it.UnitPrice, 2, MidpointRounding.AwayFromZero),
                    Subtotal = subtotal,
                    Remark = it.Remark
                });
            }

            order.SupplierId = dto.SupplierId;
            order.OrderDate = dto.OrderDate.Date;
            order.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
            order.Remark = dto.Remark;
            order.TotalAmount = decimal.Round(total, 2, MidpointRounding.AwayFromZero);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ApiResponse<object>.Success(new { id }, "采购订单已更新");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 作废（草稿/审批中 → 9）
    // ============================================================

    public async Task<ApiResponse<object>> CancelAsync(long id)
    {
        var order = await _db.PurOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("采购订单不存在", 404);
        }

        if (order.Status != (int)PurchaseOrderStatus.Draft
            && order.Status != (int)PurchaseOrderStatus.Reviewing)
        {
            return ApiResponse<object>.Fail("仅草稿/审批中状态的采购订单可作废");
        }

        order.Status = (int)PurchaseOrderStatus.Cancelled;
        await _db.SaveChangesAsync();

        // 操作日志：依赖尚未建设的"操作日志模块"，此处仅置状态；
        // 待审计模块建成后在此写入（操作人/时间/前后状态）。
        return ApiResponse<object>.Success(new { id }, "采购订单已作废");
    }

    // ============================================================
    // 确认订单（草稿 → 待入库）
    // ============================================================

    /// <summary>
    /// 确认订单：0-草稿（或历史遗留的 1-审批中）→ 2-待入库。
    /// 【无审核环节】不记录审批人/审批时间，确认后即可开入库单。
    /// </summary>
    public async Task<ApiResponse<object>> ConfirmAsync(long id)
    {
        var order = await _db.PurOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("采购订单不存在", 404);
        }

        if (order.Status != (int)PurchaseOrderStatus.Draft
            && order.Status != (int)PurchaseOrderStatus.Reviewing)
        {
            return ApiResponse<object>.Fail("仅草稿状态的采购订单可确认");
        }

        order.Status = (int)PurchaseOrderStatus.Approved;
        await _db.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id }, "订单已确认，可开始入库");
    }

    // ============================================================
    // 根据库存预警自动生成采购订单（核心接口）
    // ============================================================

    public async Task<ApiResponse<List<PurchaseOrderCreateResult>>> AutoCreateAsync(AutoCreateOrderDto dto)
    {
        // 1. 取预警集合（指定 ID 或扫描全部"启用且已触发"的预警）
        List<LogStockWarning> warnings;
        if (dto.WarningIds != null && dto.WarningIds.Count > 0)
        {
            warnings = await _warningRepository.GetListAsync(w => dto.WarningIds.Contains(w.Id));
        }
        else
        {
            warnings = await _warningRepository.GetListAsync(w => w.Status == 1);
        }

        if (warnings.Count == 0)
        {
            return ApiResponse<List<PurchaseOrderCreateResult>>.Success(new List<PurchaseOrderCreateResult>(), "没有需要生成的采购预警");
        }

        // 2. 解析每个预警对应的目标 SKU 与缺口数量
        //    - SKU 级预警（sku_id 有值）：直接取该 SKU 的库存；
        //    - 商品级预警（sku_id 为空）：取该商品下第一个启用 SKU 作为代表（简化策略，详见交付说明）。
        var skuIds = warnings.Select(w => w.SkuId ?? 0).Where(x => x > 0).Distinct().ToList();
        var prodInfoIds = warnings.Where(w => w.SkuId == null).Select(w => w.ProdInfoId).Distinct().ToList();

        var skuStock = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id) || (prodInfoIds.Contains(s.ProdInfoId) && s.Status == 1))
            .Select(s => new { s.Id, s.ProdInfoId, s.Stock, s.RetailPrice, s.Status })
            .ToListAsync();

        var skuDict = skuStock.ToDictionary(s => s.Id, s => s);

        // 按供应商分组：supplierId -> (materialId, needQty, unitPrice)
        var bySupplier = new Dictionary<long, List<(long MaterialId, decimal Qty, decimal Price)>>();

        foreach (var w in warnings)
        {
            long resolvedSkuId;
            if (w.SkuId.HasValue)
            {
                resolvedSkuId = w.SkuId.Value;
            }
            else
            {
                // 商品级预警：取该商品第一个启用 SKU 作为代表
                var rep = skuStock.FirstOrDefault(s => s.ProdInfoId == w.ProdInfoId && s.Status == 1);
                if (rep == null) continue;
                resolvedSkuId = rep.Id;
            }

            if (!skuDict.TryGetValue(resolvedSkuId, out var sku)) continue;

            var currentStock = sku.Stock;
            var need = decimal.Round(w.WarningStock - currentStock, 2, MidpointRounding.AwayFromZero);
            if (need <= 0) continue;   // 已高于阈值，无需补货

            // 供应商来自商品（prod_info.supplier_id）
            var prod = await _prodInfoRepository.GetByIdAsync(sku.ProdInfoId);
            if (prod == null || prod.SupplierId == null) continue;
            var supplierId = prod.SupplierId.Value;

            // 采购单价：系统无成本价字段，暂用零售价作为默认（接入成本价字段后替换）
            var price = decimal.Round(sku.RetailPrice, 2, MidpointRounding.AwayFromZero);

            if (!bySupplier.TryGetValue(supplierId, out var lines))
            {
                lines = new List<(long, decimal, decimal)>();
                bySupplier[supplierId] = lines;
            }

            lines.Add((resolvedSkuId, need, price));
        }

        if (bySupplier.Count == 0)
        {
            return ApiResponse<List<PurchaseOrderCreateResult>>.Success(new List<PurchaseOrderCreateResult>(), "没有可生成的采购订单（库存均已达标）");
        }

        // 3. 为每个供应商生成一张草稿采购订单（同一事务）
        var results = new List<PurchaseOrderCreateResult>();

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var (supplierId, lines) in bySupplier)
            {
                var orderNo = await PurchaseNumberHelper.GenerateAsync(
                    _db, "PO", no => _db.PurOrders.AnyAsync(o => o.OrderNo == no));

                var order = new PurOrder
                {
                    OrderNo = orderNo,
                    SupplierId = supplierId,
                    OrderDate = DateTime.Now.Date,
                    Status = (int)PurchaseOrderStatus.Draft,
                    TotalAmount = 0,
                    Remark = "库存预警自动生成"
                };
                await _db.PurOrders.AddAsync(order);
                await _db.SaveChangesAsync();

                var total = 0m;
                foreach (var (materialId, qty, price) in lines)
                {
                    var subtotal = decimal.Round(qty * price, 2, MidpointRounding.AwayFromZero);
                    total += subtotal;
                    _db.PurOrderItems.Add(new PurOrderItem
                    {
                        OrderId = order.Id,
                        MaterialId = materialId,
                        Quantity = qty,
                        UnitPrice = price,
                        Subtotal = subtotal
                    });
                }

                order.TotalAmount = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
                await _db.SaveChangesAsync();

                results.Add(new PurchaseOrderCreateResult { Id = order.Id, OrderNo = order.OrderNo });
            }

            await tx.CommitAsync();
            return ApiResponse<List<PurchaseOrderCreateResult>>.Success(results, $"已生成 {results.Count} 张采购订单");
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

    private async Task ValidateMaterialsAsync(List<PurchaseOrderItemInputDto> items)
    {
        var skuIds = items.Select(i => i.MaterialId).Distinct().ToList();
        var existing = await _skuRepository.GetListAsync(s => skuIds.Contains(s.Id));
        var missing = skuIds.FirstOrDefault(id => !existing.Any(s => s.Id == id));
        if (missing > 0)
        {
            throw new InvalidOperationException($"物料(SKU)不存在（sku_id={missing}）");
        }
    }

    private static PurchaseOrderListDto MapListDto(PurOrder o) => new()
    {
        Id = o.Id,
        OrderNo = o.OrderNo,
        SupplierId = o.SupplierId,
        SupplierName = o.Supplier?.SupplierName,
        OrderDate = o.OrderDate,
        ExpectedDeliveryDate = o.ExpectedDeliveryDate,
        Status = o.Status,
        StatusText = PurchaseOrderStatusHelper.GetName(o.Status),
        TotalAmount = o.TotalAmount,
        CreateTime = o.CreateTime
    };
}
