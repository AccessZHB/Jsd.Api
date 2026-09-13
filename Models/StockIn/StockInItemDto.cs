using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockIn;

/// <summary>
/// 入库单明细 DTO。
/// 既用于创建/编辑入库单时的嵌套入参，也用于详情/列表的明细回显。
/// 小计 Subtotal、商品名称/SKU 编码等展示字段由后端计算/填充，前端无需传。
/// </summary>
public class StockInItemDto
{
    /// <summary>明细ID（回显用；新增时传 0 或不传）</summary>
    public long Id { get; set; }

    /// <summary>入库单ID（回显用，新增时不传）</summary>
    public long StockInId { get; set; }

    /// <summary>商品ID（SPU，必传，必须是 prod_info 表中存在的商品）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品")]
    public long ProdInfoId { get; set; }

    /// <summary>
    /// SKU ID（必传，入库直接增加该 SKU 的库存 prod_sku.stock）。
    /// 数据库列允许 NULL，但本接口要求必须指定到具体 SKU。
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品SKU")]
    public long? SkuId { get; set; }

    /// <summary>入库数量（必须大于 0）</summary>
    [Range(1, int.MaxValue, ErrorMessage = "入库数量必须大于0")]
    public int Quantity { get; set; }

    /// <summary>入库成本价（不能为负）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "成本价不能为负数且不能超过上限")]
    public decimal CostPrice { get; set; }

    /// <summary>小计金额 = 数量 × 成本价（后端计算，前端传值会被覆盖）</summary>
    public decimal Subtotal { get; set; }

    /// <summary>商品名称（回显用，后端填充）</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>SKU编码（回显用，后端填充）</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU名称/规格组合文本（回显用，后端填充）</summary>
    public string? SkuName { get; set; }
}
