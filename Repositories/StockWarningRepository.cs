using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 库存预警配置仓储实现。
///
/// 关键实现：把"当前库存"用相关子查询下推到 SQL，
///   sku_id 有值 → (SELECT stock FROM prod_sku WHERE id = w.sku_id)
///   sku_id 为空 → (SELECT SUM(stock) FROM prod_sku WHERE prod_info_id = w.prod_info_id)
/// 好处是 onlyWarning（只看已触发预警）能直接在数据库层过滤，分页 total 准确，
/// 避免"先分页再内存过滤"导致的总数偏差与漏数据。
/// </summary>
public class StockWarningRepository : Repository<LogStockWarning>, IStockWarningRepository
{
    public StockWarningRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询预警配置（带当前库存；支持商品/状态/仅看预警 三种筛选）
    /// </summary>
    public async Task<(List<StockWarningWithStock> Items, int Total)> GetPagedListAsync(
        long? prodInfoId, int? status, bool? onlyWarning, int page, int pageSize)
    {
        var query = Db.LogStockWarnings
            .AsNoTracking()
            .AsQueryable();

        // 商品筛选（列表页商品下拉）
        if (prodInfoId.HasValue)
        {
            query = query.Where(w => w.ProdInfoId == prodInfoId.Value);
        }

        // 状态筛选（1-启用 0-停用）
        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        // 投影：实体 + 当前库存（相关子查询，EF Core 可翻译为 SQL）
        var projected = query.Select(w => new StockWarningWithStock
        {
            Warning = w,
            CurrentStock = w.SkuId.HasValue
                // SKU 级：取该 SKU 的库存（查不到按 0 处理）
                ? Db.ProdSkus.Where(s => s.Id == w.SkuId!.Value).Select(s => s.Stock).FirstOrDefault()
                // 商品级：取该商品全部 SKU 库存之和（无 SKU 时 SUM 为 NULL，COALESCE 成 0）
                : Db.ProdSkus.Where(s => s.ProdInfoId == w.ProdInfoId).Sum(s => (int?)s.Stock) ?? 0
        });

        // 仅看已触发预警：必须是启用状态，且当前库存 <= 预警阈值
        if (onlyWarning == true)
        {
            projected = projected.Where(x => x.Warning.Status == 1 && x.CurrentStock <= x.Warning.WarningStock);
        }

        var total = await projected.CountAsync();

        var items = await projected
            .OrderByDescending(x => x.Warning.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    /// 查询当前库存（SKU级取该SKU库存；商品级取该商品全部SKU库存之和）
    /// </summary>
    public async Task<int> GetCurrentStockAsync(long prodInfoId, long? skuId)
    {
        if (skuId.HasValue)
        {
            return await Db.ProdSkus
                .AsNoTracking()
                .Where(s => s.Id == skuId.Value)
                .Select(s => s.Stock)
                .FirstOrDefaultAsync();
        }

        return await Db.ProdSkus
            .AsNoTracking()
            .Where(s => s.ProdInfoId == prodInfoId)
            .SumAsync(s => (int?)s.Stock) ?? 0;
    }

    /// <summary>
    /// 是否已存在重复配置（同一商品 + 同一SKU 只能一条；skuId 为 null 时校验商品级配置唯一）
    /// </summary>
    public async Task<bool> ExistsConflictAsync(long prodInfoId, long? skuId, long? excludeId)
    {
        var query = Db.LogStockWarnings
            .AsNoTracking()
            .Where(w => w.ProdInfoId == prodInfoId);

        query = skuId.HasValue
            ? query.Where(w => w.SkuId == skuId.Value)
            : query.Where(w => w.SkuId == null);

        if (excludeId.HasValue)
        {
            query = query.Where(w => w.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
