using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 出库单明细 DTO。
/// 既用于创建/编辑出库单时的嵌套入参，也用于详情/列表的明细回显。
/// 出库明细仅含 商品 / SKU / 数量 / 备注（无成本价、无小计），
/// 商品名称/SKU 编码/规格组合等展示字段由后端填充，前端无需传。
/// </summary>
public class StockOutItemDto
{
    /// <summary>明细ID（回显用；新增时传 0 或不传）</summary>
    public long Id { get; set; }

    /// <summary>出库单ID（回显用，新增时不传）</summary>
    public long StockOutId { get; set; }

    /// <summary>商品ID（SPU，必传，必须是 prod_info 表中存在的商品）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品")]
    public long ProdInfoId { get; set; }

    /// <summary>
    /// SKU ID（必传，出库直接扣减该 SKU 的库存 prod_sku.stock）。
    /// 出库必须精确到具体 SKU（SKU 本身已包含规格组合 spec_values）。
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品SKU")]
    public long? SkuId { get; set; }

    /// <summary>出库数量（必须大于 0）</summary>
    [Range(1, int.MaxValue, ErrorMessage = "出库数量必须大于0")]
    public int Quantity { get; set; }

    /// <summary>明细备注（可空）</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>商品名称（回显用，后端填充）</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>SKU编码（回显用，后端填充）</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU名称/规格组合文本（回显用，后端填充）</summary>
    public string? SkuName { get; set; }
}
