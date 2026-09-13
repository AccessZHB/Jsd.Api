using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockCheck;

/// <summary>
/// 创建盘点单请求 DTO。
/// 仅录入"盘什么"（商品+SKU），系统库存由后端在事务内实时从 prod_sku.stock 快照写入，状态置为 盘点中(0)。
/// 本系统无审核环节，创建即 盘点中。
/// </summary>
public class StockCheckCreateDto
{
    /// <summary>操作人（必传）</summary>
    [Required(ErrorMessage = "操作人不能为空")]
    [StringLength(50, ErrorMessage = "操作人最长50个字符")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>备注</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>盘点明细（至少一条）</summary>
    [Required(ErrorMessage = "盘点明细不能为空")]
    [MinLength(1, ErrorMessage = "盘点明细至少一条")]
    public List<StockCheckItemInputDto> Items { get; set; } = new();
}

/// <summary>
/// 创建时单条明细入参：只传 商品 + SKU（+备注），系统库存由后端读取 prod_sku.stock。
/// </summary>
public class StockCheckItemInputDto
{
    /// <summary>商品ID（SPU，必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（必传，盘点精确到具体 SKU）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品SKU")]
    public long? SkuId { get; set; }

    /// <summary>差异原因/备注（可空）</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }
}

/// <summary>
/// 完成盘点（编辑）请求 DTO：录入每个 SKU 的实盘数量，后端据此计算盈亏并调整库存。
/// 仅 盘点中(0) 单据可调用，调用后状态置为 已完成(1)。
/// 明细按 skuId 与创建时的明细匹配（同一盘点单内 SKU 不重复），必须覆盖全部已创建明细。
/// </summary>
public class StockCheckUpdateDto
{
    /// <summary>操作人（必传）</summary>
    [Required(ErrorMessage = "操作人不能为空")]
    [StringLength(50, ErrorMessage = "操作人最长50个字符")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>备注</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>实盘明细（至少一条，须覆盖本单全部 SKU）</summary>
    [Required(ErrorMessage = "盘点明细不能为空")]
    [MinLength(1, ErrorMessage = "盘点明细至少一条")]
    public List<StockCheckActualInputDto> Items { get; set; } = new();
}

/// <summary>
/// 完成盘点时单条明细入参：按 SKU 录入实盘数量。
/// </summary>
public class StockCheckActualInputDto
{
    /// <summary>SKU ID（必传，对应本单已创建的明细）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品SKU")]
    public long SkuId { get; set; }

    /// <summary>实盘数量（0 或正数，不允许负数）</summary>
    [Range(0, int.MaxValue, ErrorMessage = "实盘数量不能为负")]
    public int ActualStock { get; set; }

    /// <summary>差异原因/备注（可空）</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }
}
