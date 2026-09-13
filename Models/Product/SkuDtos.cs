using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Jsd.Api.Models.Product;

/// <summary>
/// SKU 的一维规格（前端提交用）。
/// specId 对应 prod_spec_item.id，specValueId 对应 prod_spec_value.id；
/// 后端只取 specValueId 拼接成 "10,15" 写入 prod_sku.spec_values。
/// </summary>
public class SkuSpecValueDto
{
    /// <summary>规格项ID（prod_spec_item.id / 前端驼峰 specId，也兼容下划线 spec_id）</summary>
    [JsonPropertyName("specId")]
    public long SpecId { get; set; }

    /// <summary>
    /// 规格值ID（prod_spec_value.id / 前端驼峰 specValueId，也兼容下划线 spec_value_id）。
    /// 取值优先级最高：有它就不用再去反查。
    /// </summary>
    [JsonPropertyName("specValueId")]
    public long SpecValueId { get; set; }

    /// <summary>
    /// 规格值文本（如"1.60"）。选填，仅在 specValueId 为空或失效时作为反查兜底。
    /// </summary>
    [JsonPropertyName("specValue")]
    public string? SpecValue { get; set; }

    // ---------- 下划线风格别名（同一份值，兼容 spec_id / spec_value_id 命名习惯）----------
    // 注意：只有传入大于 0 的值才覆盖，避免"两种风格都传、其中一个为 0"时把有效值冲掉。
    /// <summary>specId 的下划线别名（前端传 spec_id 也能绑定）</summary>
    [JsonPropertyName("spec_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long SpecIdSnake
    {
        get => SpecId;
        set { if (value > 0) SpecId = value; }
    }

    /// <summary>specValueId 的下划线别名（前端传 spec_value_id 也能绑定）</summary>
    [JsonPropertyName("spec_value_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long SpecValueIdSnake
    {
        get => SpecValueId;
        set { if (value > 0) SpecValueId = value; }
    }
}

/// <summary>
/// SKU 响应 DTO（列表 / 详情通用）
/// </summary>
public class ProdSkuDto
{
    public long Id { get; set; }

    /// <summary>商品ID（prod_info_id）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>SKU编码（唯一）</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>SKU名称</summary>
    public string? SkuName { get; set; }

    /// <summary>
    /// 规格值ID组合（数据库 prod_sku.spec_values，如 "10" 或 "10,15"，对应 prod_spec_value.id）
    /// </summary>
    public string? SpecValues { get; set; }

    /// <summary>
    /// 规格值展示文本（后端按 SpecValues 里的ID反查生成，如 "1.60/防蓝光膜"）。
    /// 仅用于前端展示，不落库；反查不到时回退为 SpecValues 原值。
    /// </summary>
    public string? SpecValuesText { get; set; }

    /// <summary>
    /// 规格明细（按规格项顺序展开，便于前端回显勾选状态）。
    /// 由后端按 SpecValues 反查生成，不落库。
    /// </summary>
    public List<SkuSpecValueDto>? Specs { get; set; }

    /// <summary>零售价（原 Price）</summary>
    public decimal RetailPrice { get; set; }

    /// <summary>销售价/划线价（原 OriginalPrice）</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>库存量</summary>
    public int Stock { get; set; }

    /// <summary>销量</summary>
    public int? SalesCount { get; set; }

    /// <summary>SKU图片URL</summary>
    public string? Image { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 新增 SKU 请求 DTO。
/// 既用于独立新增接口，也嵌套在 ProdInfoCreateDto.Skus 中随 SPU 一同保存（此时 ProdInfoId 可不传，由后端填充）。
/// 【重要】不包含 SkuCode —— SKU 编码由后端 GenerateSkuCode() 自动生成，前端传值一律忽略。
/// </summary>
public class ProdSkuCreateDto
{
    /// <summary>商品ID（独立新增时必传；随 SPU 保存时可省略）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "商品ID不合法")]
    public long? ProdInfoId { get; set; }

    /// <summary>SKU名称（留空时后端用"商品名 + 规格值文本"自动补全）</summary>
    [StringLength(100)]
    public string? SkuName { get; set; }

    /// <summary>
    /// 规格组合（推荐传这个）：按规格项顺序传 [{ specId, specValueId, specValue }]，
    /// 后端取 specValueId 拼接成 "10,15" 写入 prod_sku.spec_values。
    /// 例：[{ "specId": 3, "specValueId": 10 }, { "specId": 4, "specValueId": 15 }] → "10,15"
    /// </summary>
    public List<SkuSpecValueDto>? Specs { get; set; }

    /// <summary>
    /// 规格值ID组合（前端已拼好时直接传，如 "10,15"，对应 prod_spec_value.id）。
    /// 兼容旧前端：若传的是展示文本（如 "1.60/防蓝光膜"），后端会按本次保存的规格配置反查成ID。
    /// 优先级低于 Specs（两者都传时以 Specs 为准）。
    /// </summary>
    [StringLength(255)]
    public string? SpecValues { get; set; }

    /// <summary>零售价（原 Price）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "零售价必须在 0 ~ 9999999999.99 之间")]
    public decimal RetailPrice { get; set; }

    /// <summary>销售价/划线价（原 OriginalPrice）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "销售价不能为负数")]
    public decimal? SalePrice { get; set; }

    /// <summary>库存量</summary>
    [Range(0, int.MaxValue, ErrorMessage = "库存不能为负数")]
    public int Stock { get; set; }

    /// <summary>SKU图片URL</summary>
    [StringLength(255)]
    public string? Image { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 修改 SKU 请求 DTO（改价/改库存/改规格组合）。
/// 商品归属不允许修改；sku_code 由后端生成，不允许前端修改，故此处不包含 SkuCode。
/// </summary>
public class ProdSkuUpdateDto
{
    /// <summary>SKU ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "SKU ID不合法")]
    public long Id { get; set; }

    /// <summary>SKU名称</summary>
    [StringLength(100)]
    public string? SkuName { get; set; }

    /// <summary>规格组合（与新增一致；不传表示不修改）</summary>
    public List<SkuSpecValueDto>? Specs { get; set; }

    /// <summary>
    /// 规格值ID组合（如 "10,15"）；不传表示不修改。
    /// 传了会做合法性校验：所有ID必须属于该SKU所属商品的规格值。
    /// </summary>
    [StringLength(255)]
    public string? SpecValues { get; set; }

    /// <summary>零售价（原 Price）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "零售价必须在 0 ~ 9999999999.99 之间")]
    public decimal RetailPrice { get; set; }

    /// <summary>销售价/划线价（原 OriginalPrice）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "销售价不能为负数")]
    public decimal? SalePrice { get; set; }

    /// <summary>库存量</summary>
    [Range(0, int.MaxValue, ErrorMessage = "库存不能为负数")]
    public int Stock { get; set; }

    /// <summary>SKU图片URL</summary>
    [StringLength(255)]
    public string? Image { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; } = 1;
}
