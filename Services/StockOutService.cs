using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockOut;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 出库管理服务实现。
/// 核心约定：
/// 1. 创建/编辑/删除全部使用 IDbContextTransaction 事务，主表、明细、库存、变动日志要么全成功要么全回滚；
/// 2. 库存只落在 prod_sku.stock 上，出库必须指定 sku_id；
/// 3. 库存扣减/回补采用【乐观锁（CAS 比较并交换）】：以当前库存值为期望值做条件更新
///    UPDATE prod_sku SET stock=@after WHERE id=@id AND stock=@current，
///    受影响行数=0 表示并发被其他请求修改，自动重试（最多 3 次），避免引入 row_version 列改动表结构；
/// 4. 库存校验：扣减前若 after&lt;0 直接抛「库存不足」，不进入数据库；
/// 5. 变动日志 log_stock_log.change_type=2（出库），change_qty 正增负减，
///    ref_type 区分来源：stock_out（创建）/ stock_out_update（编辑）/ stock_out_delete（删除）；
/// 6. 无审核环节，创建即完成（status=1）。
/// </summary>
public class StockOutService : IStockOutService
{
    /// <summary>变动类型：出库（对应 log_stock_log.change_type=2）</summary>
    private const int ChangeTypeOut = 2;

    /// <summary>出库单号前缀（CK = ChuKu）</summary>
    private const string NoPrefix = "CK";

    /// <summary>乐观锁 CAS 最大重试次数</summary>
    private const int MaxCasRetry = 3;

    private readonly AppDbContext _db;
    private readonly ILogStockOutRepository _stockOutRepository;
    private readonly ILogStockOutItemRepository _itemRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IRepository<ProdSku> _skuRepository;

    public StockOutService(
        AppDbContext db,
        ILogStockOutRepository stockOutRepository,
        ILogStockOutItemRepository itemRepository,
        IRepository<ProdInfo> productRepository,
        IRepository<ProdSku> skuRepository)
    {
        _db = db;
        _stockOutRepository = stockOutRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    // ============================================================
    // 查询
    // ============================================================

    /// <summary>
    /// 分页查询出库单列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<StockOutListDto>>> GetPagedListAsync(
        string? keyword, int? outType, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        // 参数兜底
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, total) = await _stockOutRepository.GetPagedListAsync(
            keyword, outType, status, startTime, endTime, page, pageSize);

        var list = orders.Select(MapToListDto).ToList();

        // 一次性统计当前页每张单的明细条数，避免 N+1
        var orderIds = list.Select(o => o.Id).ToList();
        if (orderIds.Count > 0)
        {
            var itemCounts = await _db.LogStockOutItems
                .Where(i => orderIds.Contains(i.StockOutId))
                .GroupBy(i => i.StockOutId)
                .Select(g => new { StockOutId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StockOutId, x => x.Count);

            foreach (var dto in list)
            {
                dto.ItemCount = itemCounts.TryGetValue(dto.Id, out var cnt) ? cnt : 0;
            }
        }

        return ApiResponse<PagedResult<StockOutListDto>>.Success(
            PagedResult<StockOutListDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 获取出库单详情（主表 + 明细，明细带商品名称、SKU 编码/名称）
    /// </summary>
    public async Task<ApiResponse<StockOutDetailDto>> GetDetailAsync(long id)
    {
        var order = await _stockOutRepository.GetDetailAsync(id);
        if (order == null)
        {
            return ApiResponse<StockOutDetailDto>.Fail("出库单不存在", 404);
        }

        var dto = new StockOutDetailDto
        {
            Id = order.Id,
            StockOutNo = order.StockOutNo,
            OutType = order.OutType,
            TotalQty = order.TotalQty,
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

        return ApiResponse<StockOutDetailDto>.Success(dto);
    }

    // ============================================================
    // 创建
    // ============================================================

    /// <summary>
    /// 创建出库单：写主表 → 写明细 → 乐观锁扣减 prod_sku.stock → 写 log_stock_log，全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(StockOutCreateDto dto)
    {
        // 1. 事务前先做关联校验（商品/SKU 不存在、类型非法、库存不足抛异常，由 Controller 统一转 400）
        await ValidateRelationsAsync(dto.OutType, dto.Items);

        // 2. 服务端重算主表总数量（不信任前端传入）
        var totalQty = dto.Items.Sum(i => i.Quantity);

        // 3. 生成不重复的出库单号
        var stockOutNo = await GenerateStockOutNoAsync();

        // 4. 开启事务
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 4.1 写主表（创建即完成 status=1），SaveChanges 拿到自增主键
            var order = new LogStockOut
            {
                StockOutNo = stockOutNo,
                OutType = dto.OutType,
                TotalQty = totalQty,
                Status = 1,
                Remark = dto.Remark,
                Operator = dto.Operator
            };
            await _stockOutRepository.AddAsync(order);
            await _stockOutRepository.SaveChangesAsync();

            // 4.2 构建明细 + 逐 SKU 乐观锁扣减库存 + 写变动日志
            // stockCache 记录本事务内已知的最新库存，保证同一 SKU 多次出现时顺序扣减
            var stockCache = new Dictionary<long, int>();
            var logList = new List<LogStockLog>();
            var itemList = new List<LogStockOutItem>();

            foreach (var itemDto in dto.Items)
            {
                itemList.Add(new LogStockOutItem
                {
                    StockOutId = order.Id,
                    ProdInfoId = itemDto.ProdInfoId,
                    SkuId = itemDto.SkuId,
                    Quantity = itemDto.Quantity,
                    Remark = itemDto.Remark
                });

                // 扣减库存并写日志（delta 为负数）
                await AdjustStockAndLogAsync(
                    logList, stockCache,
                    itemDto.ProdInfoId, itemDto.SkuId, -itemDto.Quantity,
                    "stock_out", order.Id, "出库扣减", dto.Operator);
            }

            // 4.3 明细、日志随库存更新一次性提交
            await _itemRepository.AddRangeAsync(itemList);
            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = order.Id, stockOutNo = order.StockOutNo }, "出库成功");
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
    /// 编辑出库单：
    /// 旧明细回补库存（写回补日志）→ 更新主表 → 删旧明细 → 写新明细并扣减库存（写出库日志），全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(long id, StockOutUpdateDto dto)
    {
        // 1. 主表必须存在（GetByIdAsync 为跟踪查询，后续字段修改可直接保存）
        var order = await _stockOutRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("出库单不存在", 404);
        }

        // 2. 关联校验
        await ValidateRelationsAsync(dto.OutType, dto.Items);

        var totalQty = dto.Items.Sum(i => i.Quantity);

        // 3. 开启事务
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 3.1 查询旧明细，逐 SKU 回补库存并记录回补日志（delta 为正数）
            var oldItems = await _itemRepository.GetByStockOutIdAsync(id);
            var stockCache = new Dictionary<long, int>();
            var logList = new List<LogStockLog>();

            foreach (var old in oldItems)
            {
                if (!old.SkuId.HasValue)
                {
                    continue;
                }

                await AdjustStockAndLogAsync(
                    logList, stockCache,
                    old.ProdInfoId, old.SkuId, old.Quantity,
                    "stock_out_update", id, "编辑出库单-回补原明细", dto.Operator);
            }

            // 3.2 删除旧明细（与库存更新同一 SaveChanges 提交）
            if (oldItems.Count > 0)
            {
                await _itemRepository.RemoveByStockOutIdAsync(id);
            }

            // 3.3 更新主表字段
            order.OutType = dto.OutType;
            order.Remark = dto.Remark;
            order.Operator = dto.Operator;
            order.TotalQty = totalQty;
            order.Status = 1;

            // 3.4 写入新明细，并逐 SKU 扣减库存、写日志（delta 为负数）
            var newItemList = new List<LogStockOutItem>();
            foreach (var itemDto in dto.Items)
            {
                newItemList.Add(new LogStockOutItem
                {
                    StockOutId = id,
                    ProdInfoId = itemDto.ProdInfoId,
                    SkuId = itemDto.SkuId,
                    Quantity = itemDto.Quantity,
                    Remark = itemDto.Remark
                });

                await AdjustStockAndLogAsync(
                    logList, stockCache,
                    itemDto.ProdInfoId, itemDto.SkuId, -itemDto.Quantity,
                    "stock_out_update", id, "编辑出库单-按新明细出库", dto.Operator);
            }

            await _itemRepository.AddRangeAsync(newItemList);
            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id, stockOutNo = order.StockOutNo }, "出库单修改成功");
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
    /// 删除出库单：按明细回补库存 → 删除明细与主表（物理删除），全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var order = await _stockOutRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("出库单不存在", 404);
        }

        var items = await _itemRepository.GetByStockOutIdAsync(id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 逐 SKU 回补库存（delta 为正数，写回补日志）
            var stockCache = new Dictionary<long, int>();
            var logList = new List<LogStockLog>();
            foreach (var item in items)
            {
                if (!item.SkuId.HasValue)
                {
                    continue;
                }

                await AdjustStockAndLogAsync(
                    logList, stockCache,
                    item.ProdInfoId, item.SkuId, item.Quantity,
                    "stock_out_delete", id, "删除出库单-回补库存", order.Operator);
            }

            // 2. 删除明细与主表，随库存回补一起提交
            if (items.Count > 0)
            {
                await _itemRepository.RemoveByStockOutIdAsync(id);
            }
            _stockOutRepository.Remove(order);
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
    /// 关联数据校验：出库类型合法性、商品存在性、SKU 存在性及 SKU 归属。
    /// 不通过时抛出 InvalidOperationException（中文消息），由 Controller 统一捕获返回 400。
    /// 出库无供应商字段，故不校验供应商。
    /// </summary>
    private async Task ValidateRelationsAsync(int outType, List<StockOutItemDto> items)
    {
        // 出库类型必须在 1-样品领用 2-报损 3-其他 范围内
        if (outType is < 1 or > 3)
        {
            throw new InvalidOperationException("出库类型不合法（1-样品领用 2-报损 3-其他）");
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
    /// 乐观锁（CAS 比较并交换）调整 SKU 库存并写变动日志。
    /// delta &lt; 0 表示扣减（出库），delta &gt; 0 表示回补（编辑/删除）。
    /// 以当前库存为期望值做条件更新，受影响行数=0 视为并发冲突并重试（最多 MaxCasRetry 次）。
    /// </summary>
    private async Task AdjustStockAndLogAsync(
        List<LogStockLog> logs,
        Dictionary<long, int> stockCache,
        long prodInfoId, long? skuId, int delta,
        string refType, long refId, string remark, string opUser)
    {
        if (!skuId.HasValue)
        {
            throw new InvalidOperationException("出库/回补必须指定 SKU");
        }

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            // 取当前库存：优先用本事务已更新的缓存值，否则从库实时读取
            var current = stockCache.TryGetValue(skuId.Value, out var cached)
                ? cached
                : await _db.ProdSkus.AsNoTracking()
                    .Where(s => s.Id == skuId.Value)
                    .Select(s => s.Stock)
                    .FirstAsync();

            var after = current + delta;

            // 库存校验：扣减不得导致负库存
            if (after < 0)
            {
                throw new InvalidOperationException(
                    $"SKU(sku_id={skuId}) 库存不足（当前 {current}，需扣 {Math.Abs(delta)}）");
            }

            // 乐观锁：仅当库内 stock 仍等于读取到的 current 时才更新
            var affected = await _db.ProdSkus
                .Where(s => s.Id == skuId.Value && s.Stock == current)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, after));

            if (affected == 1)
            {
                // 更新本事务库存缓存，供同 SKU 后续明细顺序计算
                stockCache[skuId.Value] = after;

                logs.Add(new LogStockLog
                {
                    ProdInfoId = prodInfoId,
                    SkuId = skuId,
                    ChangeType = ChangeTypeOut,
                    ChangeQty = delta,
                    BeforeStock = current,
                    AfterStock = after,
                    RefType = refType,
                    RefId = refId,
                    Remark = remark,
                    Operator = opUser
                });
                return;
            }
            // affected==0：被其他请求并发修改，循环重新读取最新库存后重试
        }

        throw new InvalidOperationException($"SKU(sku_id={skuId}) 库存更新并发冲突，请重试");
    }

    /// <summary>
    /// 生成出库单号：CK + yyyyMMddHHmmss + 4位随机数（如 CK202609101430251234）。
    /// 极端情况下撞号（数据库 uk_stock_out_no 唯一索引兜底）则重试，最多 5 次。
    /// </summary>
    private async Task<string> GenerateStockOutNoAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var no = NoPrefix
                + DateTime.Now.ToString("yyyyMMddHHmmss")
                + Random.Shared.Next(0, 10000).ToString("D4");

            if (!await _stockOutRepository.StockOutNoExistsAsync(no))
            {
                return no;
            }
        }

        throw new InvalidOperationException("出库单号生成失败（号段冲突），请重试");
    }

    /// <summary>实体 → 列表 DTO（列表不带明细）</summary>
    private static StockOutListDto MapToListDto(LogStockOut o) => new()
    {
        Id = o.Id,
        StockOutNo = o.StockOutNo,
        OutType = o.OutType,
        TotalQty = o.TotalQty,
        Status = o.Status,
        Remark = o.Remark,
        Operator = o.Operator,
        CreateTime = o.CreateTime,
        UpdateTime = o.UpdateTime
    };

    /// <summary>明细实体 → DTO（仅标量字段，展示名称由调用方批量回填）</summary>
    private static StockOutItemDto MapToItemDto(LogStockOutItem i) => new()
    {
        Id = i.Id,
        StockOutId = i.StockOutId,
        ProdInfoId = i.ProdInfoId,
        SkuId = i.SkuId,
        Quantity = i.Quantity,
        Remark = i.Remark
    };
}
