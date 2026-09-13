using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockIn;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 入库管理服务实现。
/// 核心约定：
/// 1. 创建/编辑/删除全部使用 IDbContextTransaction 事务，主表、明细、库存、变动日志要么全成功要么全回滚；
/// 2. 库存只落在 prod_sku.stock 上，入库必须指定 sku_id；
/// 3. 变动日志 log_stock_log.change_type 为 TINYINT（1=入库），
///    业务来源存在 ref_type（stock_in=采购入库 / stock_in_update=编辑入库单），change_qty 正增负减；
/// 4. 无审核环节，创建即完成（status=1）。
/// </summary>
public class StockInService : IStockInService
{
    /// <summary>变动类型：入库（对应 log_stock_log.change_type=1）</summary>
    private const int ChangeTypeIn = 1;

    private readonly AppDbContext _db;
    private readonly ILogStockInRepository _stockInRepository;
    private readonly ILogStockInItemRepository _itemRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IRepository<ProdSku> _skuRepository;
    private readonly IRepository<SupSupplier> _supplierRepository;

    public StockInService(
        AppDbContext db,
        ILogStockInRepository stockInRepository,
        ILogStockInItemRepository itemRepository,
        IRepository<ProdInfo> productRepository,
        IRepository<ProdSku> skuRepository,
        IRepository<SupSupplier> supplierRepository)
    {
        _db = db;
        _stockInRepository = stockInRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _skuRepository = skuRepository;
        _supplierRepository = supplierRepository;
    }

    // ============================================================
    // 查询
    // ============================================================

    /// <summary>
    /// 分页查询入库单列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<StockInListDto>>> GetPagedListAsync(
        string? keyword, long? supplierId, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        // 参数兜底
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, total) = await _stockInRepository.GetPagedListAsync(
            keyword, supplierId, status, startTime, endTime, page, pageSize);

        var list = orders.Select(MapToListDto).ToList();

        // 一次性统计当前页每张单的明细条数，避免 N+1
        var orderIds = list.Select(o => o.Id).ToList();
        if (orderIds.Count > 0)
        {
            var itemCounts = await _db.LogStockInItems
                .Where(i => orderIds.Contains(i.StockInId))
                .GroupBy(i => i.StockInId)
                .Select(g => new { StockInId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StockInId, x => x.Count);

            foreach (var dto in list)
            {
                dto.ItemCount = itemCounts.TryGetValue(dto.Id, out var cnt) ? cnt : 0;
            }
        }

        return ApiResponse<PagedResult<StockInListDto>>.Success(
            PagedResult<StockInListDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 获取入库单详情（主表 + 明细，明细带商品名称、SKU 编码/名称）
    /// </summary>
    public async Task<ApiResponse<StockInDetailDto>> GetDetailAsync(long id)
    {
        var order = await _stockInRepository.GetDetailAsync(id);
        if (order == null)
        {
            return ApiResponse<StockInDetailDto>.Fail("入库单不存在", 404);
        }

        var dto = new StockInDetailDto
        {
            Id = order.Id,
            StockInNo = order.StockInNo,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.SupplierName,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            Remark = order.Remark,
            Operator = order.Operator,
            CreateTime = order.CreateTime,
            UpdateTime = order.UpdateTime,
            ItemCount = order.Items.Count
        };

        // 批量查商品与 SKU，回填展示字段（避免 N+1）
        var prodIds = order.Items.Select(i => i.ProdInfoId).Distinct().ToList();
        var skuIds = order.Items.Where(i => i.SkuId.HasValue).Select(i => i.SkuId!.Value).Distinct().ToList();

        var productMap = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ProdInfoName);

        var skuMap = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        dto.Items = order.Items.Select(i =>
        {
            var itemDto = MapToItemDto(i);
            itemDto.ProdInfoName = productMap.TryGetValue(i.ProdInfoId, out var pn) ? pn : null;
            if (i.SkuId.HasValue && skuMap.TryGetValue(i.SkuId.Value, out var sku))
            {
                itemDto.SkuCode = sku.SkuCode;
                itemDto.SkuName = sku.SkuName;
            }
            return itemDto;
        }).ToList();

        return ApiResponse<StockInDetailDto>.Success(dto);
    }

    // ============================================================
    // 创建
    // ============================================================

    /// <summary>
    /// 创建入库单：写主表 → 写明细 → 增加 prod_sku.stock → 写 log_stock_log，全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(StockInCreateDto dto)
    {
        // 1. 事务前先做关联校验（商品/SKU/供应商不存在直接抛异常，由 Controller 统一转 400）
        await ValidateRelationsAsync(dto.SupplierId, dto.Items);

        // 2. 服务端重算每条明细的小计，并汇总主表总金额（不信任前端传入的 subtotal）
        var totalAmount = dto.Items.Sum(i => CalcSubtotal(i.Quantity, i.CostPrice));

        // 3. 生成不重复的入库单号
        var stockInNo = await GenerateStockInNoAsync();

        // 4. 开启事务
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 4.1 写主表（创建即完成 status=1），SaveChanges 拿到自增主键
            var order = new LogStockIn
            {
                StockInNo = stockInNo,
                SupplierId = dto.SupplierId,
                TotalAmount = totalAmount,
                Status = 1,
                Remark = dto.Remark,
                Operator = dto.Operator
            };
            await _stockInRepository.AddAsync(order);
            await _stockInRepository.SaveChangesAsync();

            // 4.2 构建明细 + 逐 SKU 增加库存 + 写变动日志
            // skuCache 保证同一事务内同一 SKU 只被 ChangeTracker 跟踪一次，before/after 顺序计算正确
            var skuCache = new Dictionary<long, ProdSku>();
            var logList = new List<LogStockLog>();
            var itemList = new List<LogStockInItem>();

            foreach (var itemDto in dto.Items)
            {
                var sku = await GetTrackedSkuAsync(itemDto.SkuId!.Value, skuCache);

                // 库存变动：stock = stock + quantity
                var beforeStock = sku.Stock;
                sku.Stock = beforeStock + itemDto.Quantity;

                itemList.Add(new LogStockInItem
                {
                    StockInId = order.Id,
                    ProdInfoId = itemDto.ProdInfoId,
                    SkuId = itemDto.SkuId,
                    Quantity = itemDto.Quantity,
                    CostPrice = itemDto.CostPrice,
                    Subtotal = CalcSubtotal(itemDto.Quantity, itemDto.CostPrice)
                });

                logList.Add(BuildStockLog(
                    itemDto.ProdInfoId, itemDto.SkuId, itemDto.Quantity,
                    beforeStock, sku.Stock, "stock_in", order.Id, "采购入库", dto.Operator));
            }

            // 4.3 明细、日志随库存更新一次性提交
            await _itemRepository.AddRangeAsync(itemList);
            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = order.Id, stockInNo = order.StockInNo }, "入库成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 编辑
    // ============================================================

    /// <summary>
    /// 编辑入库单：
    /// 旧明细冲减库存（写冲减日志）→ 更新主表 → 删旧明细 → 写新明细并增加库存（写入库日志），全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(long id, StockInUpdateDto dto)
    {
        // 1. 主表必须存在（FindAsync 为跟踪查询，后续字段修改可直接保存）
        var order = await _stockInRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("入库单不存在", 404);
        }

        // 2. 关联校验
        await ValidateRelationsAsync(dto.SupplierId, dto.Items);

        var totalAmount = dto.Items.Sum(i => CalcSubtotal(i.Quantity, i.CostPrice));

        // 3. 开启事务
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 3.1 查询旧明细（跟踪状态），逐 SKU 冲减库存并记录冲减日志
            var oldItems = await _itemRepository.GetByStockInIdAsync(id);
            var skuCache = new Dictionary<long, ProdSku>();
            var logList = new List<LogStockLog>();

            foreach (var old in oldItems)
            {
                if (!old.SkuId.HasValue)
                {
                    continue;
                }

                var sku = await GetTrackedSkuAsync(old.SkuId.Value, skuCache);

                // 冲减：stock = stock - 旧数量（change_qty 记负数）
                var beforeStock = sku.Stock;
                sku.Stock = beforeStock - old.Quantity;

                logList.Add(BuildStockLog(
                    old.ProdInfoId, old.SkuId, -old.Quantity,
                    beforeStock, sku.Stock, "stock_in_update", id,
                    "编辑入库单-冲减原明细", dto.Operator));
            }

            // 3.2 删除旧明细（先标记删除，与库存更新同一 SaveChanges 提交）
            if (oldItems.Count > 0)
            {
                _db.LogStockInItems.RemoveRange(oldItems);
            }

            // 3.3 更新主表字段
            order.SupplierId = dto.SupplierId;
            order.Remark = dto.Remark;
            order.Operator = dto.Operator;
            order.TotalAmount = totalAmount;
            order.Status = 1;

            // 3.4 写入新明细，并逐 SKU 增加库存、写日志
            var newItemList = new List<LogStockInItem>();
            foreach (var itemDto in dto.Items)
            {
                var sku = await GetTrackedSkuAsync(itemDto.SkuId!.Value, skuCache);

                var beforeStock = sku.Stock;
                sku.Stock = beforeStock + itemDto.Quantity;

                newItemList.Add(new LogStockInItem
                {
                    StockInId = id,
                    ProdInfoId = itemDto.ProdInfoId,
                    SkuId = itemDto.SkuId,
                    Quantity = itemDto.Quantity,
                    CostPrice = itemDto.CostPrice,
                    Subtotal = CalcSubtotal(itemDto.Quantity, itemDto.CostPrice)
                });

                logList.Add(BuildStockLog(
                    itemDto.ProdInfoId, itemDto.SkuId, itemDto.Quantity,
                    beforeStock, sku.Stock, "stock_in_update", id,
                    "编辑入库单-按新明细入库", dto.Operator));
            }

            await _itemRepository.AddRangeAsync(newItemList);
            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id, stockInNo = order.StockInNo }, "入库单修改成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 删除
    // ============================================================

    /// <summary>
    /// 删除入库单：按明细冲减库存 → 删除明细与主表（物理删除），全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var order = await _stockInRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("入库单不存在", 404);
        }

        var items = await _itemRepository.GetByStockInIdAsync(id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 逐 SKU 冲减库存（同一 SKU 多次出现时通过缓存顺序扣减）
            var skuCache = new Dictionary<long, ProdSku>();
            foreach (var item in items)
            {
                if (!item.SkuId.HasValue)
                {
                    continue;
                }

                var sku = await GetTrackedSkuAsync(item.SkuId.Value, skuCache);
                sku.Stock -= item.Quantity;
            }

            // 2. 删除明细与主表，随库存更新一起提交
            if (items.Count > 0)
            {
                _db.LogStockInItems.RemoveRange(items);
            }
            _stockInRepository.Remove(order);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id }, "删除成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    /// <summary>
    /// 关联数据校验：供应商存在性、商品存在性、SKU 存在性及 SKU 归属。
    /// 不通过时抛出 InvalidOperationException（中文消息），由 Controller 统一捕获返回 400。
    /// </summary>
    private async Task ValidateRelationsAsync(long? supplierId, List<StockInItemDto> items)
    {
        if (supplierId.HasValue &&
            !await _supplierRepository.AnyAsync(s => s.Id == supplierId.Value))
        {
            throw new InvalidOperationException("所选供应商不存在");
        }

        // 去重后批量校验，减少数据库往返
        var prodIds = items.Select(i => i.ProdInfoId).Distinct().ToList();
        var existingProdIds = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();
        var missingProdId = prodIds.FirstOrDefault(pid => !existingProdIds.Contains(pid));
        if (missingProdId > 0)
        {
            throw new InvalidOperationException($"商品不存在（prod_info_id={missingProdId}）");
        }

        var skuIds = items.Where(i => i.SkuId.HasValue).Select(i => i.SkuId!.Value).Distinct().ToList();
        var skuList = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Select(s => new { s.Id, s.ProdInfoId })
            .ToListAsync();

        var skuDict = skuList.ToDictionary(s => s.Id, s => s.ProdInfoId);
        foreach (var item in items)
        {
            var skuId = item.SkuId!.Value;
            if (!skuDict.TryGetValue(skuId, out var skuProdInfoId))
            {
                throw new InvalidOperationException($"SKU不存在（sku_id={skuId}）");
            }

            // 防止前端把别的商品的 SKU 挂到当前明细上
            if (skuProdInfoId != item.ProdInfoId)
            {
                throw new InvalidOperationException(
                    $"SKU与商品不匹配（sku_id={skuId} 不属于 prod_info_id={item.ProdInfoId}）");
            }
        }
    }

    /// <summary>
    /// 获取被 ChangeTracker 跟踪的 SKU 实体：
    /// 同一事务内同一 SKU 复用同一实例，保证 before/after 库存按顺序累计计算。
    /// </summary>
    private async Task<ProdSku> GetTrackedSkuAsync(long skuId, Dictionary<long, ProdSku> cache)
    {
        if (cache.TryGetValue(skuId, out var cached))
        {
            return cached;
        }

        // 不加 AsNoTracking：需要 EF 跟踪，SaveChanges 时生成 UPDATE prod_sku SET stock=...
        var sku = await _db.ProdSkus.FirstOrDefaultAsync(s => s.Id == skuId)
                 ?? throw new InvalidOperationException($"SKU不存在（sku_id={skuId}）");

        cache[skuId] = sku;
        return sku;
    }

    /// <summary>
    /// 构造库存变动日志
    /// </summary>
    private static LogStockLog BuildStockLog(
        long prodInfoId, long? skuId, int changeQty,
        int beforeStock, int afterStock,
        string refType, long refId, string remark, string opUser)
    {
        return new LogStockLog
        {
            ProdInfoId = prodInfoId,
            SkuId = skuId,
            ChangeType = ChangeTypeIn,
            ChangeQty = changeQty,
            BeforeStock = beforeStock,
            AfterStock = afterStock,
            RefType = refType,
            RefId = refId,
            Remark = remark,
            Operator = opUser
        };
    }

    /// <summary>
    /// 生成入库单号：RK + yyyyMMddHHmmss + 4位随机数（如 RK202609101430251234）。
    /// 极端情况下撞号（数据库 uk_stock_in_no 唯一索引兜底）则重试，最多 5 次。
    /// </summary>
    private async Task<string> GenerateStockInNoAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var no = "RK"
                + DateTime.Now.ToString("yyyyMMddHHmmss")
                + Random.Shared.Next(0, 10000).ToString("D4");

            if (!await _stockInRepository.StockInNoExistsAsync(no))
            {
                return no;
            }
        }

        throw new InvalidOperationException("入库单号生成失败（号段冲突），请重试");
    }

    /// <summary>计算明细小计：数量 × 成本价（保留 2 位小数）</summary>
    private static decimal CalcSubtotal(int quantity, decimal costPrice)
        => Math.Round(quantity * costPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>实体 → 列表 DTO（列表不带明细）</summary>
    private static StockInListDto MapToListDto(LogStockIn o) => new()
    {
        Id = o.Id,
        StockInNo = o.StockInNo,
        SupplierId = o.SupplierId,
        SupplierName = o.Supplier?.SupplierName,
        TotalAmount = o.TotalAmount,
        Status = o.Status,
        Remark = o.Remark,
        Operator = o.Operator,
        CreateTime = o.CreateTime,
        UpdateTime = o.UpdateTime
    };

    /// <summary>明细实体 → DTO（仅标量字段，展示名称由调用方批量回填）</summary>
    private static StockInItemDto MapToItemDto(LogStockInItem i) => new()
    {
        Id = i.Id,
        StockInId = i.StockInId,
        ProdInfoId = i.ProdInfoId,
        SkuId = i.SkuId,
        Quantity = i.Quantity,
        CostPrice = i.CostPrice,
        Subtotal = i.Subtotal
    };
}
