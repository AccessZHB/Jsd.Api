using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 应付账款表 pur_payable（采购管理模块）
///
/// 记录向供应商的应付货款，与财务模块的 mkt_payment（客户应收）形成对称：
///   - mkt_payment 管"客户欠我们的钱"（应收）
///   - pur_payable 管"我们欠供应商的钱"（应付）
///
/// 入库单确认后自动生成应付记录；后续通过线下付款进行核销（PayAsync）。
/// </summary>
[Table("pur_payable")]
public class PurPayable
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>应付单号（唯一，格式 AP-yyyyMMdd-001，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("payable_no")]
    public string PayableNo { get; set; } = string.Empty;

    /// <summary>供应商ID（关联 sup_supplier.id）</summary>
    [Column("supplier_id")]
    public long SupplierId { get; set; }

    /// <summary>关联采购订单ID（pur_order.id，可空）</summary>
    [Column("related_order_id")]
    public long? RelatedOrderId { get; set; }

    /// <summary>应付总额（入库明细实收数量 × 采购单价之和）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>已付金额（付款核销时累加）</summary>
    [Column("paid_amount", TypeName = "decimal(12,2)")]
    public decimal PaidAmount { get; set; }

    /// <summary>未付金额（total_amount - paid_amount）</summary>
    [Column("unpaid_amount", TypeName = "decimal(12,2)")]
    public decimal UnpaidAmount { get; set; }

    /// <summary>到期付款日（可空）</summary>
    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    /// <summary>状态：0-未付 1-部分付款 2-已结清</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>供应商（查询时 Include）</summary>
    public SupSupplier? Supplier { get; set; }

    /// <summary>关联采购订单（查询时 Include）</summary>
    public PurOrder? Order { get; set; }
}
