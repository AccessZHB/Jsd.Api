using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockQuery;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 库存查询服务实现（纯只读，5 张现有表关联查询）。
///
/// 【预警判定规则】（与库存预警模块保持一致）
/// 1. SKU 级配置（log_stock_warning.sku_id 有值）优先：用该 SKU 自身库存与阈值比较；
/// 2. 无 SKU 级配置时回退商品级配置（sku_id 为空）：用该商品【全部 SKU 库存之和】与阈值比较，
///    命中则该商品下所有 SKU 均标记为预警；
/// 3. 只有 status=1（启用）的配置参与判定；未配置任何预警的 SKU 不参与预警统计。
///
/// 【分页与筛选顺序】库存状态（预警/正常/缺货）依赖实时计算，无法直接下推到 SQL，
/// 因此采用"轻量投影 → 内存计算预警 → 筛选 → 取本页 ID → 按需查明细"的两段式查询，
/// 既保证分页 total 准确，又避免复杂子查询在 MySQL 上的翻译风险。
/// </summary>
public class StockQueryService : IStockQueryService
{
    /// <summary>总览看板展示的最近变动条数</summary>
    private const int RecentLogCount = 10;

    private readonly AppDbContext _db;
    private readonly IStockQueryRepository _queryRepository;

    public StockQueryService(AppDbContext db, IStockQueryRepository queryRepository)
    {
        _db = db;
        _queryRepository = queryRepository;
    }

    // ============================================================
    // 1. 库存总览
    // ============================================================

    /// <summary>
    /// 库存总览：商品总数 / SKU总数 / 库存总量 / 预警数 / 缺货数 + 最近 10 条变动
    /// </summary>
    public async Task<ApiResponse<StockOverviewDto>> GetOverviewAsync()
    {
        var productCount = await _queryRepository.GetProductCountAsync();

        // 全量 SKU 轻量投影（只有 3 个字段，一次查询供 4 项统计复用）
        var allRows = await _queryRepository.GetSkuStockRowsAsync(null, null, null);
        var warnings = await _queryRepository.GetEnabledWarningsAsync();

        var (skuWarnMap, prodWarnMap) = BuildWarningMaps(warnings);

        // 商品级预警需要"该商品全部 SKU 库存之和"
        var productTotalMap = allRows
            .GroupBy(r => r.ProdInfoId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Stock));

        var warningCount = allRows.Count(r =>
            ComputeWarning(r.SkuId, r.ProdInfoId, r.Stock, skuWarnMap, prodWarnMap, productTotalMap).IsWarning);

        var dto = new StockOverviewDto
        {
            ProductCount = productCount,
            SkuCount = allRows.Count,
            TotalStock = allRows.Sum(r => r.Stock),
            WarningCount = warningCount,
            OutOfStockCount = allRows.Count(r => r.Stock == 0)
        };

        // 最近变动（含商品/SKU名称）
        var recentLogs = await _queryRepository.GetRecentLogsAsync(RecentLogCount);
        dto.RecentLogs = await BuildLogDtosAsync(recentLogs);

        return ApiResponse<StockOverviewDto>.Success(dto);
    }

    // ============================================================
    // 2. 库存明细
    // ============================================================

    /// <summary>
    /// 库存明细：SKU 维度分页查询（多条件筛选 + 预警状态标记）
    /// </summary>
    public async Task<ApiResponse<PagedResult<StockDetailItemDto>>> GetDetailAsync(
        string? keyword, long? prodInfoId, long? supplierId, int stockStatus, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        // 1) 轻量投影：应用可下推 SQL 的筛选（关键词/商品/供应商）
        var rows = await _queryRepository.GetSkuStockRowsAsync(keyword, prodInfoId, supplierId);
        var warnings = await _queryRepository.GetEnabledWarningsAsync();
        var (skuWarnMap, prodWarnMap) = BuildWarningMaps(warnings);

        // 2) 商品总库存：必须按商品全量统计，不能直接用上面筛选后的 rows 求和
        var prodIds = rows.Select(r => r.ProdInfoId).Distinct().ToList();
        var productTotalMap = prodIds.Count == 0
            ? new Dictionary<long, int>()
            : await _db.ProdSkus.AsNoTracking()
                .Where(s => prodIds.Contains(s.ProdInfoId))
                .GroupBy(s => s.ProdInfoId)
                .Select(g => new { ProdInfoId = g.Key, Total = g.Sum(x => x.Stock) })
                .ToDictionaryAsync(x => x.ProdInfoId, x => x.Total);

        // 3) 内存计算预警状态
        var computed = rows.Select(r =>
        {
            var (threshold, isWarning) = ComputeWarning(
                r.SkuId, r.ProdInfoId, r.Stock, skuWarnMap, prodWarnMap, productTotalMap);
            return new SkuComputedRow
            {
                SkuId = r.SkuId,
                Stock = r.Stock,
                WarningStock = threshold,
                IsWarning = isWarning,
                IsOutOfStock = r.Stock == 0
            };
        }).ToList();

        // 4) 库存状态筛选
        IEnumerable<SkuComputedRow> filtered = computed;
        if (stockStatus == StockStatusFilters.OutOfStock)
        {
            filtered = computed.Where(x => x.IsOutOfStock);
        }
        else if (stockStatus == StockStatusFilters.Warning)
        {
            filtered = computed.Where(x => x.IsWarning);
        }
        else if (stockStatus == StockStatusFilters.Normal)
        {
            filtered = computed.Where(x => !x.IsWarning && !x.IsOutOfStock);
        }

        var total = filtered.Count();
        var pageItems = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 5) 只查本页 ID 的完整明细（关联商品 + 供应商）
        var pageIds = pageItems.Select(x => x.SkuId).ToList();
        var detailRows = await _queryRepository.GetDetailRowsByIdsAsync(pageIds);
        var detailMap = detailRows.ToDictionary(d => d.SkuId);

        // 6) 组装 DTO（保持与 pageIds 相同的顺序）
        var list = new List<StockDetailItemDto>();
        foreach (var id in pageIds)
        {
            if (!detailMap.TryGetValue(id, out var d)) continue;
            var c = pageItems.First(x => x.SkuId == id);
            list.Add(new StockDetailItemDto
            {
                SkuId = d.SkuId,
                SkuCode = d.SkuCode,
                SkuName = d.SkuName,
                SpecValues = d.SpecValues,
                Stock = d.Stock,
                RetailPrice = d.RetailPrice,
                ProdInfoId = d.ProdInfoId,
                ProdInfoName = d.ProdInfoName,
                ProdInfoCode = d.ProdInfoCode,
                Unit = d.Unit,
                SupplierName = d.SupplierName,
                WarningStock = c.WarningStock,
                IsWarning = c.IsWarning,
                IsOutOfStock = c.IsOutOfStock
            });
        }

        return ApiResponse<PagedResult<StockDetailItemDto>>.Success(
            PagedResult<StockDetailItemDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 3. 变动日志
    // ============================================================

    /// <summary>变动日志：分页查询（类型/商品/关键词/日期范围 筛选）</summary>
    public async Task<ApiResponse<PagedResult<StockLogItemDto>>> GetLogAsync(
        int? changeType, long? prodInfoId, string? keyword,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, total) = await _queryRepository.GetLogPagedAsync(
            changeType, prodInfoId, keyword, startTime, endTime, page, pageSize);

        var list = await BuildLogDtosAsync(items);

        return ApiResponse<PagedResult<StockLogItemDto>>.Success(
            PagedResult<StockLogItemDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    /// <summary>
    /// 明细行中间结果：SKU 库存 + 实时计算出的预警状态。
    /// 定义成具体类（而非匿名类型/dynamic），保证筛选与组装环节都是强类型。
    /// </summary>
    private class SkuComputedRow
    {
        /// <summary>SKU ID</summary>
        public long SkuId { get; set; }

        /// <summary>当前库存</summary>
        public int Stock { get; set; }

        /// <summary>命中的预警阈值（未配置 = null）</summary>
        public int? WarningStock { get; set; }

        /// <summary>是否触发预警</summary>
        public bool IsWarning { get; set; }

        /// <summary>是否缺货（库存 = 0）</summary>
        public bool IsOutOfStock { get; set; }
    }

    /// <summary>
    /// 构建预警阈值字典。用索引器赋值而非 ToDictionary，避免脏数据重复键导致抛异常。
    /// </summary>
    private static (Dictionary<long, int> SkuWarnMap, Dictionary<long, int> ProdWarnMap)
        BuildWarningMaps(List<LogStockWarning> warnings)
    {
        var skuWarnMap = new Dictionary<long, int>();
        var prodWarnMap = new Dictionary<long, int>();

        foreach (var w in warnings)
        {
            if (w.SkuId.HasValue)
            {
                // SKU 级预警：sku_id → 阈值
                skuWarnMap[w.SkuId.Value] = w.WarningStock;
            }
            else
            {
                // 商品级预警：prod_info_id → 阈值（按商品总库存判定）
                prodWarnMap[w.ProdInfoId] = w.WarningStock;
            }
        }

        return (skuWarnMap, prodWarnMap);
    }

    /// <summary>
    /// 预警判定：SKU 级配置优先，否则回退商品级配置（按商品全部 SKU 库存之和比较）。
    /// 返回 (阈值, 是否预警)；未配置阈值时阈值 = null、是否预警 = false。
    /// </summary>
    private static (int? Threshold, bool IsWarning) ComputeWarning(
        long skuId, long prodInfoId, int skuStock,
        Dictionary<long, int> skuWarnMap,
        Dictionary<long, int> prodWarnMap,
        Dictionary<long, int> productTotalMap)
    {
        // 1) SKU 级：用该 SKU 自身库存比较
        if (skuWarnMap.TryGetValue(skuId, out var skuThreshold))
        {
            return (skuThreshold, skuStock <= skuThreshold);
        }

        // 2) 商品级：用该商品全部 SKU 库存之和比较
        if (prodWarnMap.TryGetValue(prodInfoId, out var prodThreshold)
            && productTotalMap.TryGetValue(prodInfoId, out var productTotal))
        {
            return (prodThreshold, productTotal <= prodThreshold);
        }

        // 3) 未配置预警
        return (null, false);
    }

    /// <summary>
    /// 变动日志实体 → DTO（批量回填商品名称与 SKU 编码，避免 N+1）
    /// </summary>
    private async Task<List<StockLogItemDto>> BuildLogDtosAsync(List<LogStockLog> logs)
    {
        if (logs.Count == 0)
        {
            return new List<StockLogItemDto>();
        }

        var prodIds = logs.Select(l => l.ProdInfoId).Distinct().ToList();
        var skuIds = logs
            .Where(l => l.SkuId.HasValue)
            .Select(l => l.SkuId!.Value)
            .Distinct()
            .ToList();

        var productMap = await _db.ProdInfos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ProdInfoName);

        var skuMap = skuIds.Count == 0
            ? new Dictionary<long, ProdSku>()
            : await _db.ProdSkus.AsNoTracking()
                .Where(s => skuIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

        return logs.Select(l => new StockLogItemDto
        {
            Id = l.Id,
            ProdInfoId = l.ProdInfoId,
            ProdInfoName = productMap.TryGetValue(l.ProdInfoId, out var pname) ? pname : null,
            SkuId = l.SkuId,
            SkuCode = l.SkuId.HasValue && skuMap.TryGetValue(l.SkuId.Value, out var sku) ? sku.SkuCode : null,
            ChangeType = l.ChangeType,
            ChangeTypeName = StockChangeTypes.GetName(l.ChangeType),
            ChangeQty = l.ChangeQty,
            BeforeStock = l.BeforeStock,
            AfterStock = l.AfterStock,
            RefType = l.RefType,
            RefId = l.RefId,
            Remark = l.Remark,
            Operator = l.Operator,
            CreateTime = l.CreateTime
        }).ToList();
    }
}
