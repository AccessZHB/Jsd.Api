using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 库存盘点单主表 log_stock_check（依据 Jsd_order.sql 逐列映射）
/// 状态：0-盘点中 1-已完成。本系统无审核环节：创建即 盘点中，录入实盘即 已完成。
/// 与入库/出库单不同：盘点单没有供应商、没有金额字段，差异由明细汇总得出。
/// </summary>
[Table("log_stock_check")]
public class LogStockCheck
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>盘点单号（PD + yyyyMMddHHmmss + 4位随机数，数据库有唯一索引）</summary>
    [Required]
    [StringLength(32)]
    [Column("check_no")]
    public string CheckNo { get; set; } = string.Empty;

    /// <summary>状态：0-盘点中 1-已完成</summary>
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

    /// <summary>盘点明细集合</summary>
    public virtual List<LogStockCheckItem> Items { get; set; } = new();
}
