using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 库存查询只读仓储接口。
///
/// 本模块是纯查询模块，涉及的 5 张表（log_stock_log / prod_sku / prod_info / sup_supplier /
/// log_stock_warning）全部是已有表，因此 Entity 层无需新建实体，直接复用既有实体做关联查询。
/// 也不继承泛型 IRepository&lt;T&gt;（不需要增删改），仅暴露查询方法。
/// </summary>
public interface IStockQueryRepository
{
    /// <summary>商品总数（prod_info）</summary>
    Task<int> GetProductCountAsync();

    /// <summary>
    /// 查询 SKU 库存轻量投影（只有 ID / 商品ID / 库存 三个字段）。
    /// 三个筛选条件都可空：全传 null 即返回全量（总览统计复用此方法）。
    /// </summary>
    /// <param name="keyword">关键词：匹配商品名称 / 商品编码 / SKU编码</param>
    /// <param name="prodInfoId">商品ID</param>
    /// <param name="supplierId">供应商ID</param>
    Task<List<SkuStockRow>> GetSkuStockRowsAsync(string? keyword, long? prodInfoId, long? supplierId);

    /// <summary>按 SKU ID 批量取明细行（关联商品 + 供应商），用于分页取当前页数据</summary>
    Task<List<StockDetailRow>> GetDetailRowsByIdsAsync(List<long> skuIds);

    /// <summary>查询启用中的预警配置（用于计算预警状态与预警数）</summary>
    Task<List<LogStockWarning>> GetEnabledWarningsAsync();

    /// <summary>最近 N 条变动日志（总览看板用，按 ID 倒序）</summary>
    Task<List<LogStockLog>> GetRecentLogsAsync(int count);

    /// <summary>
    /// 分页查询变动日志（类型 / 商品 / 关键词 / 日期范围 筛选）
    /// </summary>
    Task<(List<LogStockLog> Items, int Total)> GetLogPagedAsync(
        int? changeType, long? prodInfoId, string? keyword,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);
}

/// <summary>
/// SKU 库存轻量投影（只取计算所需字段，用于统计与预警判定，避免查全表字段）
/// </summary>
public class SkuStockRow
{
    /// <summary>SKU ID</summary>
    public long SkuId { get; set; }

    /// <summary>商品ID</summary>
    public long ProdInfoId { get; set; }

    /// <summary>当前库存</summary>
    public int Stock { get; set; }
}

/// <summary>
/// 库存明细行（prod_sku 关联 prod_info、sup_supplier 后的平铺结果）
/// </summary>
public class StockDetailRow
{
    /// <summary>SKU ID</summary>
    public long SkuId { get; set; }

    /// <summary>SKU 编码</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU 名称</summary>
    public string? SkuName { get; set; }

    /// <summary>规格组合文本</summary>
    public string? SpecValues { get; set; }

    /// <summary>当前库存</summary>
    public int Stock { get; set; }

    /// <summary>零售价</summary>
    public decimal RetailPrice { get; set; }

    /// <summary>商品ID</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>商品编码</summary>
    public string? ProdInfoCode { get; set; }

    /// <summary>计量单位</summary>
    public string? Unit { get; set; }

    /// <summary>供应商名称（可为空 = 无供应商）</summary>
    public string? SupplierName { get; set; }
}
