using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockCheck;

/// <summary>
/// 盘点明细 DTO（与 log_stock_check_item 逐列对应）。
/// system_stock 为创建时带入的系统库存快照；actual_stock 为实盘数量；diff_qty 为盈亏（实盘-系统）。
/// 商品名称 / SKU 编码 / 规格组合等展示字段由后端批量回填，前端无需传。
/// </summary>
public class StockCheckItemDto
{
    /// <summary>明细ID（回显用；新增时传 0 或不传）</summary>
    public long Id { get; set; }

    /// <summary>盘点单ID（回显用）</summary>
    public long CheckId { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（必填；盘点精确到具体 SKU）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品SKU")]
    public long? SkuId { get; set; }

    /// <summary>系统库存（盘点前快照，创建时由后端写入 prod_sku.stock）</summary>
    public int SystemStock { get; set; }

    /// <summary>实盘数量（盘点时录入；盘点中为 0）</summary>
    public int ActualStock { get; set; }

    /// <summary>盈亏数量 = actual_stock - system_stock（正为盈、负为亏）</summary>
    public int DiffQty { get; set; }

    /// <summary>差异原因（可空）</summary>
    [StringLength(255, ErrorMessage = "差异原因最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>商品名称（回显用，后端填充）</summary>
    public string? ProdInfoName { get; set; }

    /// <summary>SKU编码（回显用，后端填充）</summary>
    public string? SkuCode { get; set; }

    /// <summary>SKU名称/规格组合文本（回显用，后端填充）</summary>
    public string? SkuName { get; set; }
}
