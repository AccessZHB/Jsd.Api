using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 库存查询只读仓储实现（直接持有 AppDbContext，不继承泛型 Repository&lt;T&gt;——本模块无增删改）。
///
/// 关联关系（全部为已有表，不新建实体）：
///   prod_sku.prod_info_id → prod_info.id
///   prod_info.supplier_id → sup_supplier.id（可空，左连接）
///   log_stock_log.prod_info_id / sku_id → 商品 / SKU（名称由 Service 批量回填）
///   log_stock_warning（status=1 启用中的配置）→ 预警阈值来源
/// </summary>
public class StockQueryRepository : IStockQueryRepository
{
    private readonly AppDbContext _db;

    public StockQueryRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>商品总数（prod_info 全量，不按上架状态过滤）</summary>
    public async Task<int> GetProductCountAsync()
    {
        return await _db.ProdInfos.AsNoTracking().CountAsync();
    }

    /// <summary>
    /// 查询 SKU 库存轻量投影：只取 ID / 商品ID / 库存 三个字段。
    /// 总览统计（全量）与明细筛选（带条件）共用此方法，条件全传 null 即返回全量。
    /// </summary>
    public async Task<List<SkuStockRow>> GetSkuStockRowsAsync(
        string? keyword, long? prodInfoId, long? supplierId)
    {
        // prod_sku 内连接 prod_info：关键词与供应商筛选都需要商品侧字段
        var query = from s in _db.ProdSkus.AsNoTracking()
                    join p in _db.ProdInfos.AsNoTracking() on s.ProdInfoId equals p.Id
                    select new { S = s, P = p };

        // 关键词：商品名称 / 商品编码 / SKU编码
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.P.ProdInfoName.Contains(keyword)
                || (x.P.ProdInfoCode != null && x.P.ProdInfoCode.Contains(keyword))
                || x.S.SkuCode.Contains(keyword));
        }

        if (prodInfoId.HasValue)
        {
            query = query.Where(x => x.S.ProdInfoId == prodInfoId.Value);
        }

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.P.SupplierId == supplierId.Value);
        }

        return await query
            .OrderByDescending(x => x.S.Id)
            .Select(x => new SkuStockRow
            {
                SkuId = x.S.Id,
                ProdInfoId = x.S.ProdInfoId,
                Stock = x.S.Stock
            })
            .ToListAsync();
    }

    /// <summary>
    /// 按 SKU ID 批量取明细行（关联商品、左连接供应商）
    /// </summary>
    public async Task<List<StockDetailRow>> GetDetailRowsByIdsAsync(List<long> skuIds)
    {
        if (skuIds.Count == 0)
        {
            return new List<StockDetailRow>();
        }

        return await (from s in _db.ProdSkus.AsNoTracking()
                      join p in _db.ProdInfos.AsNoTracking() on s.ProdInfoId equals p.Id
                      join sup in _db.SupSuppliers.AsNoTracking() on p.SupplierId equals (long?)sup.Id into gj
                      from sup in gj.DefaultIfEmpty()
                      where skuIds.Contains(s.Id)
                      select new StockDetailRow
                      {
                          SkuId = s.Id,
                          SkuCode = s.SkuCode,
                          SkuName = s.SkuName,
                          SpecValues = s.SpecValues,
                          Stock = s.Stock,
                          RetailPrice = s.RetailPrice,
                          ProdInfoId = p.Id,
                          ProdInfoName = p.ProdInfoName,
                          ProdInfoCode = p.ProdInfoCode,
                          Unit = p.Unit,
                          SupplierName = sup != null ? sup.SupplierName : null
                      })
            .ToListAsync();
    }

    /// <summary>启用中的预警配置（预警阈值来源；停用配置不参与判定）</summary>
    public async Task<List<LogStockWarning>> GetEnabledWarningsAsync()
    {
        return await _db.LogStockWarnings
            .AsNoTracking()
            .Where(w => w.Status == 1)
            .ToListAsync();
    }

    /// <summary>最近 N 条变动日志（总览看板，按 ID 倒序）</summary>
    public async Task<List<LogStockLog>> GetRecentLogsAsync(int count)
    {
        return await _db.LogStockLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Id)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>分页查询变动日志（类型 / 商品 / 关键词 / 日期范围 筛选）</summary>
    public async Task<(List<LogStockLog> Items, int Total)> GetLogPagedAsync(
        int? changeType, long? prodInfoId, string? keyword,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        var query = _db.LogStockLogs
            .AsNoTracking()
            .AsQueryable();

        // 变动类型
        if (changeType.HasValue)
        {
            query = query.Where(l => l.ChangeType == changeType.Value);
        }

        // 商品
        if (prodInfoId.HasValue)
        {
            query = query.Where(l => l.ProdInfoId == prodInfoId.Value);
        }

        // 关键词：匹配操作人 / 备注
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(l =>
                (l.Operator != null && l.Operator.Contains(keyword))
                || (l.Remark != null && l.Remark.Contains(keyword)));
        }

        // 日期范围（按天查询时 endTime 传当天 23:59:59）
        if (startTime.HasValue)
        {
            query = query.Where(l => l.CreateTime >= startTime.Value);
        }
        if (endTime.HasValue)
        {
            var end = endTime.Value.TimeOfDay == TimeSpan.Zero
                ? endTime.Value.AddDays(1).AddSeconds(-1)
                : endTime.Value;
            query = query.Where(l => l.CreateTime <= end);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
