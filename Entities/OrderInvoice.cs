using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 订单开票记录表 order_invoice（发票与税务管理模块）
///
/// 【状态机 status】
///   0-申请中 Applying ──财务开票（回写发票代码/号码）──&gt; 1-已开票 Issued
///   0-申请中 ──撤销申请──&gt; 2-已作废 Canceled
///   1-已开票 ──红冲──&gt; 2-已作废 Canceled（须填红冲原因 + 关联蓝票号码）
///
/// 【为什么抬头/税号要冗余快照】
///   发票一经开具即具备法律效力，抬头必须与开票当时的申请一致；
///   客户之后修改 customer_invoice_info 不能影响已开票记录，故开票时整体快照一份。
///
/// 【幂等】同一订单已有「申请中 / 已开票」记录时不允许重复申请（需先作废原票）。
/// 【金额】total_amount 为价税合计（元），tax_amount 为税额（元），与 trx_order.pay_amount 同量纲。
/// </summary>
[Table("order_invoice")]
public class OrderInvoice
{
    /// <summary>ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联订单ID（trx_order.id）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>关联支付单ID（payment_order.id；线下付款/信用订单可为空）</summary>
    [Column("payment_id")]
    public long? PaymentId { get; set; }

    /// <summary>发票类型（见 InvoiceType：1-增值税电子普通发票 2-增值税专用发票）</summary>
    [Column("invoice_type")]
    public int InvoiceType { get; set; }

    // ==================== 开票信息快照 ====================

    /// <summary>抬头类型（见 InvoiceTitleType）</summary>
    [Column("title_type")]
    public int TitleType { get; set; }

    /// <summary>抬头名称</summary>
    [Required]
    [StringLength(100)]
    [Column("title_name")]
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号</summary>
    [StringLength(32)]
    [Column("tax_no")]
    public string? TaxNo { get; set; }

    /// <summary>开户银行</summary>
    [StringLength(100)]
    [Column("bank_name")]
    public string? BankName { get; set; }

    /// <summary>银行账号</summary>
    [StringLength(64)]
    [Column("bank_account")]
    public string? BankAccount { get; set; }

    /// <summary>注册地址</summary>
    [StringLength(200)]
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>联系电话</summary>
    [StringLength(32)]
    [Column("phone")]
    public string? Phone { get; set; }

    // ==================== 票面信息（财务开票后回写） ====================

    /// <summary>发票代码（全电发票无代码，可留空）</summary>
    [StringLength(32)]
    [Column("invoice_code")]
    public string? InvoiceCode { get; set; }

    /// <summary>发票号码（唯一，防重复录入）</summary>
    [StringLength(64)]
    [Column("invoice_no")]
    public string? InvoiceNo { get; set; }

    /// <summary>开票日期</summary>
    [Column("invoice_date")]
    public DateTime? InvoiceDate { get; set; }

    /// <summary>发票总金额（价税合计，单位：元）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>税额（单位：元）</summary>
    [Column("tax_amount", TypeName = "decimal(12,2)")]
    public decimal TaxAmount { get; set; }

    // ==================== 状态与红冲 ====================

    /// <summary>开票状态（见 InvoiceStatus：0-申请中 1-已开票 2-已作废）</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>红冲 / 作废原因</summary>
    [StringLength(200)]
    [Column("red_reason")]
    public string? RedReason { get; set; }

    /// <summary>关联蓝票发票号码（红冲时被红冲的那张蓝票号码）</summary>
    [StringLength(64)]
    [Column("blue_invoice_no")]
    public string? BlueInvoiceNo { get; set; }

    // ==================== 留痕 ====================

    /// <summary>申请人ID（0 表示后台代客申请）</summary>
    [Column("applicant_id")]
    public long ApplicantId { get; set; }

    /// <summary>申请人名称</summary>
    [StringLength(64)]
    [Column("applicant_name")]
    public string? ApplicantName { get; set; }

    /// <summary>开票/作废操作人ID（财务）</summary>
    [Column("operator_id")]
    public long OperatorId { get; set; }

    /// <summary>操作人名称</summary>
    [StringLength(64)]
    [Column("operator_name")]
    public string? OperatorName { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（申请时间）</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
