using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品入库单主表 log_stock_in（依据 Jsd_order.sql 逐列映射）
/// 状态：0-待入库 1-已完成 2-已取消。本系统无审核环节，创建即完成（status=1）。
/// </summary>
[Table("log_stock_in")]
public class LogStockIn
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>入库单号（RK + yyyyMMddHHmmss + 4位随机数，数据库有唯一索引）</summary>
    [Required]
    [StringLength(32)]
    [Column("stock_in_no")]
    public string StockInNo { get; set; } = string.Empty;

    /// <summary>供应商ID（可空 = 无供应商，关联 sup_supplier.id）</summary>
    [Column("supplier_id")]
    public long? SupplierId { get; set; }

    /// <summary>入库总成本金额（各明细 subtotal 之和，服务端计算）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>状态：0-待入库 1-已完成 2-已取消</summary>
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

    /// <summary>供应商导航属性（用于 Include 查供应商名称）</summary>
    [ForeignKey(nameof(SupplierId))]
    public virtual SupSupplier? Supplier { get; set; }

    /// <summary>入库明细集合</summary>
    public virtual List<LogStockInItem> Items { get; set; } = new();
}
