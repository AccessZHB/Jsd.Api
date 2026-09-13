using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockIn;

/// <summary>
/// 创建入库单请求 DTO。
/// 一次性提交主表字段 + 明细列表，后端在同一事务中写主表、明细，
/// 并逐条增加 prod_sku.stock、写 log_stock_log 变动日志。
/// 本系统无审核环节，创建即完成（status=1）。
/// </summary>
public class StockInCreateDto
{
    /// <summary>供应商ID（可空 = 无供应商；传了必须在 sup_supplier 表中存在）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "供应商ID不合法")]
    public long? SupplierId { get; set; }

    /// <summary>备注</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>操作人（必传）</summary>
    [Required(ErrorMessage = "操作人不能为空")]
    [StringLength(50, ErrorMessage = "操作人最长50个字符")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>入库明细列表（至少一条）</summary>
    [Required(ErrorMessage = "入库明细不能为空")]
    [MinLength(1, ErrorMessage = "入库明细至少一条")]
    public List<StockInItemDto> Items { get; set; } = new();
}

/// <summary>
/// 编辑入库单请求 DTO（路由传入库单ID，Body 结构与创建一致）。
/// 库存处理：先按旧明细冲减库存 → 删旧明细 → 写新明细 → 按新明细增加库存，全过程事务。
/// </summary>
public class StockInUpdateDto : StockInCreateDto
{
}
