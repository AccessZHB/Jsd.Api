using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品出库单主表 log_stock_out（依据 Jsd_order.sql 逐列映射）
/// 状态：0-待出库 1-已完成。本系统无审核环节，创建即完成（status=1）。
/// 与入库单不同：出库单无供应商、无金额字段，主表以 total_qty（出库总数量）汇总。
/// </summary>
[Table("log_stock_out")]
public class LogStockOut
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>出库单号（CK + yyyyMMddHHmmss + 4位随机数，数据库有唯一索引）</summary>
    [Required]
    [StringLength(32)]
    [Column("stock_out_no")]
    public string StockOutNo { get; set; } = string.Empty;

    /// <summary>出库类型：1-样品领用 2-报损 3-其他</summary>
    [Column("out_type")]
    public int OutType { get; set; }

    /// <summary>出库总数量（各明细 quantity 之和，服务端计算）</summary>
    [Column("total_qty")]
    public int TotalQty { get; set; }

    /// <summary>状态：0-待出库 1-已完成</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>操作人</summary>
    [Required]
    [StringLength(50)]
    [Column("operator")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>出库明细集合</summary>
    public virtual List<LogStockOutItem> Items { get; set; } = new();
}
