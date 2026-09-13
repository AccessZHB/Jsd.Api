namespace Jsd.Api.Models.StockQuery;

// ============================================================
// 常量 / 枚举
// ============================================================

/// <summary>
/// 变动类型（对应 log_stock_log.change_type，TINYINT）
/// 1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他
/// </summary>
public static class StockChangeTypes
{
    /// <summary>入库</summary>
    public const int In = 1;

    /// <summary>出库</summary>
    public const int Out = 2;

    /// <summary>盘点调整</summary>
    public const int Check = 3;

    /// <summary>订单扣减</summary>
    public const int Order = 4;

    /// <summary>其他</summary>
    public const int Other = 5;

    /// <summary>变动类型 → 中文名（前端 Tag 展示）</summary>
    public static string GetName(int changeType) => changeType switch
    {
        In => "入库",
        Out => "出库",
        Check => "盘点调整",
        Order => "订单扣减",
        Other => "其他",
        _ => "未知"
    };
}

/// <summary>
/// 库存明细筛选：库存状态（前端下拉）
/// 0-全部 1-正常 2-预警 3-缺货
/// </summary>
public static class StockStatusFilters
{
    /// <summary>全部</summary>
    public const int All = 0;

    /// <summary>正常（有配置但未触发 / 无配置）</summary>
    public const int Normal = 1;

    /// <summary>预警（当前库存 &lt;= 预警阈值）</summary>
    public const int Warning = 2;

    /// <summary>缺货（库存 = 0）</summary>
    public const int OutOfStock = 3;
}

// ============================================================
// 1. 库存总览（GET /api/stockQuery/overview）
// ============================================================

/// <summary>
/// 库存总览看板数据：5 个统计指标 + 最近变动记录。
/// 【统计口径】商品总数取 prod_info 全量；SKU 相关指标（总数/库存总量/预警数/缺货数）
/// 统一以 prod_sku 全量为基数，不按上架状态过滤，保证"库存总量"与"SKU总数"口径一致。
/// </summary>
public class StockOverviewDto
{
    /// <summary>商品总数（prod_info）</summary>
    public int ProductCount { get; set; }

    /// <summary>SKU 总数（prod_sku）</summary>
    public int SkuCount { get; set; }

    /// <summary>库存总量（所有 SKU 的 stock 之和）</summary>
    public int TotalStock { get; set; }

    /// <summary>预警 SKU 数（触发了启用中预警配置的 SKU 数量）</summary>
    public int WarningCount { get; set; }

    /// <summary>缺货 SKU 数（stock = 0）</summary>
    public int OutOfStockCount { get; set; }

    /// <summary>最近变动记录（默认最近 10 条）</summary>
    public List<StockLogItemDto> RecentLogs { get; set; } = new();
}

// ============================================================
// 2. 库存明细（GET /api/stockQuery/detail）
// ============================================================

/// <summary>
/// SKU 库存明修行（prod_sku 关联 prod_info、sup_supplier）。
/// isWarning / warningStock 由 Service 结合 log_stock_warning 实时计算后填充。
/// </summary>
public class StockDetailItemDto
{
    /// <summary>SKU ID</summary>
    public long SkuId { get; set; }

    /// <summary>SKU 编码</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU 名称</summary>
    public string? SkuName { get; set; }

    /// <summary>规格组合文本，如 "1.60/防蓝光膜"</summary>
    public string? SpecValues { get; set; }

    /// <summary>当前库存</summary>
    public int Stock { get; set; }

    /// <summary>零售价（prod_sku.retail_price）</summary>
    public decimal RetailPrice { get; set; }

    /// <summary>商品ID（SPU）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>商品编码</summary>
    public string? ProdInfoCode { get; set; }

    /// <summary>计量单位（片/副/盒）</summary>
    public string? Unit { get; set; }

    /// <summary>供应商名称（prod_info.supplier_id → sup_supplier.supplier_name，可空）</summary>
    public string? SupplierName { get; set; }

    /// <summary>预警阈值（未配置预警时为 null）</summary>
    public int? WarningStock { get; set; }

    /// <summary>是否触发预警（有启用中的配置 且 库存 &lt;= 阈值）</summary>
    public bool IsWarning { get; set; }

    /// <summary>是否缺货（库存 = 0）</summary>
    public bool IsOutOfStock { get; set; }
}

// ============================================================
// 3. 变动日志（GET /api/stockQuery/log）
// ============================================================

/// <summary>
/// 库存变动日志行（log_stock_log，关联 prod_info / prod_sku 补名称）。
/// changeQty 正为增加、负为减少（前端据此做正负号与颜色区分）。
/// </summary>
public class StockLogItemDto
{
    /// <summary>日志ID</summary>
    public long Id { get; set; }

    /// <summary>商品ID</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称（回显字段）</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>SKU ID（可空）</summary>
    public long? SkuId { get; set; }

    /// <summary>SKU 编码（回显字段，可空）</summary>
    public string? SkuCode { get; set; }

    /// <summary>变动类型：1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他</summary>
    public int ChangeType { get; set; }

    /// <summary>变动类型中文名（回显字段）</summary>
    public string? ChangeTypeName { get; set; }

    /// <summary>变动数量（正=增加，负=减少）</summary>
    public int ChangeQty { get; set; }

    /// <summary>变动前库存</summary>
    public int BeforeStock { get; set; }

    /// <summary>变动后库存</summary>
    public int AfterStock { get; set; }

    /// <summary>关联单据类型：stock_in / stock_out / check / order</summary>
    public string? RefType { get; set; }

    /// <summary>关联单据ID</summary>
    public long? RefId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>操作人</summary>
    public string? Operator { get; set; }

    /// <summary>变动时间</summary>
    public DateTime CreateTime { get; set; }
}
