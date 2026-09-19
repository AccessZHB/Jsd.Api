using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 应收账款表 trx_receivable（B2B 赊账 / 月结业务）
///
/// 【业务定位】
///   记录每个"信用订单"产生的应收款项。与 trx_payment_log（线上微信支付回调）互补：
///   本表管的是"客户欠我多少钱"，mkt_payment 管的是"客户实际还了多少钱"。
///   支持一笔收款拆分核销多张应收单（通过 mkt_payment_item 关联）。
///
/// 【生成时机】
///   订单【发货】且 trx_order.is_credit = 1（信用订单）时，由 ReceivableService.SyncFromOrderAsync
///   自动生成一条应收记录。普通订单（is_credit=0）不产生应收。
///
/// 【金额口径】
///   unpaid_amount = total_amount - paid_amount（三个字段始终自洽，由核销逻辑维护）
///
/// 【状态 status】（严格遵循建表注释）
///   0-未结清  1-已结清  2-已逾期
///   注意：建表只定义 3 个状态，"部分结清"不是独立的状态码，
///   而是由 paid_amount &gt; 0 且 unpaid_amount &gt; 0 推导出的展示态（见 ReceivableStatus.GetSettleName）。
/// </summary>
[Table("trx_receivable")]
public class TrxReceivable
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联订单ID (trx_order.id)</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>客户ID (mem_member.id)</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    // ==================== 金额 ====================

    /// <summary>应收总额（取订单实付金额 trx_order.pay_amount）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>已收款金额（累计核销金额，由核销逻辑累加）</summary>
    [Column("paid_amount", TypeName = "decimal(12,2)")]
    public decimal PaidAmount { get; set; }

    /// <summary>未收款金额 = total_amount - paid_amount</summary>
    [Column("unpaid_amount", TypeName = "decimal(12,2)")]
    public decimal UnpaidAmount { get; set; }

    // ==================== 日期 / 账期 ====================

    /// <summary>订单日期（用于账龄计算）</summary>
    [Column("order_date", TypeName = "date")]
    public DateTime OrderDate { get; set; }

    /// <summary>到期日 = 订单日期 + 客户账期天数（mem_member.account_period）</summary>
    [Column("due_date", TypeName = "date")]
    public DateTime? DueDate { get; set; }

    // ==================== 状态 / 备注 ====================

    /// <summary>状态：0-未结清 1-已结清 2-已逾期</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    // ==================== 时间戳 ====================

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
