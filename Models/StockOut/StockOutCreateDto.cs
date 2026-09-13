using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 创建出库单请求 DTO。
/// 一次性提交主表字段（类型/备注/操作人）+ 明细列表，后端在同一事务中
/// 写主表、明细，逐条扣减 prod_sku.stock（乐观锁），并写 log_stock_log 变动日志。
/// 本系统无审核环节，创建即完成（status=1）。
/// </summary>
public class StockOutCreateDto
{
    /// <summary>
    /// 出库类型：1-样品领用 2-报损 3-其他（必传，必须在合法范围内）
    /// </summary>
    [Range(1, 3, ErrorMessage = "出库类型不合法（1-样品领用 2-报损 3-其他）")]
    public int OutType { get; set; }

    /// <summary>备注</summary>
    [StringLength(255, ErrorMessage = "备注最长255个字符")]
    public string? Remark { get; set; }

    /// <summary>操作人（必传）</summary>
    [Required(ErrorMessage = "操作人不能为空")]
    [StringLength(50, ErrorMessage = "操作人最长50个字符")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>出库明细列表（至少一条）</summary>
    [Required(ErrorMessage = "出库明细不能为空")]
    [MinLength(1, ErrorMessage = "出库明细至少一条")]
    public List<StockOutItemDto> Items { get; set; } = new();
}

/// <summary>
/// 编辑出库单请求 DTO（路由传入库单ID，Body 结构与创建一致）。
/// 库存处理：先按旧明细回补库存 → 删旧明细 → 写新明细 → 按新明细扣减库存，全过程事务。
/// </summary>
public class StockOutUpdateDto : StockOutCreateDto
{
}
