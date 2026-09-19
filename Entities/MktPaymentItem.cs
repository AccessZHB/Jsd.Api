using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 收款核销关联表 mkt_payment_item
///
/// 【业务定位】
///   解决"一笔收款对应多张订单"的多对多核销场景。
///   管理员录入一笔收款后，可选择多个应收账款进行核销拆分，实现灵活的财务对账。
///
/// 【写入时机】
///   仅由 PaymentService.WriteOffAsync 在事务中批量插入；
///   插入的同时会回写 trx_receivable 的 paid_amount / unpaid_amount / status。
/// </summary>
[Table("mkt_payment_item")]
public class MktPaymentItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联收款记录ID (mkt_payment.id)</summary>
    [Column("payment_id")]
    public long PaymentId { get; set; }

    /// <summary>关联应收账款ID (trx_receivable.id)</summary>
    [Column("receivable_id")]
    public long ReceivableId { get; set; }

    /// <summary>该笔收款用于此订单的核销金额（元）</summary>
    [Column("offset_amount", TypeName = "decimal(12,2)")]
    public decimal OffsetAmount { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    // ==================== 导航属性 ====================
    // 反向导航必须显式标注 [ForeignKey]，否则 EF Core 会生成 PaymentId1 / ReceivableId1 之类的影子外键列。

    /// <summary>所属收款单</summary>
    [ForeignKey(nameof(PaymentId))]
    public virtual MktPayment? Payment { get; set; }

    /// <summary>被核销的应收单</summary>
    [ForeignKey(nameof(ReceivableId))]
    public virtual TrxReceivable? Receivable { get; set; }
}
