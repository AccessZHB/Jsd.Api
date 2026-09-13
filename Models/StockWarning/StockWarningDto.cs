using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockWarning;

/// <summary>
/// 库存预警配置项（列表 / 详情返回）。
///
/// 【预警检测逻辑】本表只存"配置"，不存预警状态；
/// 是否触发预警由 Service 实时计算后填充 IsWarning：
///     IsWarning = (Status == 1 启用) 且 (CurrentStock &lt;= WarningStock)
/// 即：停用的配置一律不参与预警判定（IsWarning 恒为 false），
///     商品当前库存 <= 预警阈值 时触发。
///
/// CurrentStock 的取法（对应 sku_id 是否为空）：
///     sku_id 有值 → SKU 级预警，CurrentStock = 该 SKU 的 prod_sku.stock
///     sku_id 为空 → 商品级预警，CurrentStock = 该商品全部 SKU 的 stock 之和
/// </summary>
public class StockWarningItemDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（关联 prod_sku.id）；null = 商品级预警</summary>
    public long? SkuId { get; set; }

    /// <summary>预警阈值（库存 &lt;= 此值时触发预警）</summary>
    public int WarningStock { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    // ==================== 关联展示字段（由 Service 批量回填，避免 N+1） ====================

    /// <summary>商品名称（prod_info.prod_info_name）</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>商品编码（prod_info.prod_info_code）</summary>
    public string? ProdInfoCode { get; set; }

    /// <summary>SKU 编码（商品级预警为 null）</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU 名称（商品级预警为 null）</summary>
    public string? SkuName { get; set; }

    /// <summary>规格组合文本，如 "1.60/防蓝光膜"（商品级预警为 null）</summary>
    public string? SpecValues { get; set; }

    /// <summary>当前库存（SKU级=该SKU库存；商品级=该商品所有SKU库存之和）</summary>
    public int CurrentStock { get; set; }

    /// <summary>是否已触发预警（仅启用配置参与判定）</summary>
    public bool IsWarning { get; set; }
}

/// <summary>
/// 新增 / 编辑预警配置入参（POST /api/stockWarning、PUT /api/stockWarning 共用）。
/// Id 为空或 0 视为新增，否则为编辑。
///
/// 【参数校验逻辑】除 DataAnnotations 的声明式校验外，Service 层还会校验：
/// 1. 商品必须存在（prod_info）；
/// 2. SKU 若传入必须存在，且必须属于该商品（防止跨商品挂接）；
/// 3. 预警阈值不能为负数；
/// 4. 同一商品 + 同一 SKU 只能有一条配置（编辑时排除自身）。
/// </summary>
public class StockWarningSaveDto
{
    /// <summary>主键ID：null / 0 = 新增；有值 = 编辑</summary>
    public long? Id { get; set; }

    /// <summary>商品ID（SPU，必填）</summary>
    [Required(ErrorMessage = "请选择商品")]
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（可空；为空表示商品级预警，按该商品全部 SKU 库存合计判定）</summary>
    public long? SkuId { get; set; }

    /// <summary>预警阈值（库存 &lt;= 此值触发；不能为负）</summary>
    [Range(0, 1000000, ErrorMessage = "预警阈值必须为 0 ~ 1000000 之间的整数")]
    public int WarningStock { get; set; } = 10;

    /// <summary>状态：1-启用 0-停用（默认启用）</summary>
    [Range(0, 1, ErrorMessage = "状态值非法（0-停用 1-启用）")]
    public int Status { get; set; } = 1;
}

/// <summary>
/// 启用 / 停用切换入参（PUT /api/stockWarning/enable/{id}）。
/// Status 传值则设为指定状态；不传则按当前状态取反（切换）。
/// </summary>
public class StockWarningEnableDto
{
    /// <summary>目标状态：1-启用 0-停用；不传 = 取反切换</summary>
    [Range(0, 1, ErrorMessage = "状态值非法（0-停用 1-启用）")]
    public int? Status { get; set; }
}
