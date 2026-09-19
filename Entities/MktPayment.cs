using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 收款记录表 mkt_payment（线下收款：银行转账 / 支票 / 现金 / 其他）
///
/// 【业务定位】
///   与 trx_payment_log（微信支付回调日志）互补，分别对应"线下收款"和"线上支付"两条资金流。
///   一笔收款可以拆分核销到多张应收单，拆分关系记录在 mkt_payment_item。
///
/// 【状态流转 status】
///   0 待核销 ──核销──&gt; 1 已核销
///   核销由 PaymentService.WriteOffAsync 完成，要求"核销明细总金额 == 本单 amount"（必须刚好核销完）。
///
/// 【收款单号】payment_no 唯一，格式：PAY + yyyyMMddHHmmss + 4位随机数（后端自动生成）。
/// </summary>
[Table("mkt_payment")]
public class MktPayment
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>收款单号（唯一，格式：PAY + yyyyMMddHHmmss + 4位随机数）</summary>
    [Required]
    [StringLength(32)]
    [Column("payment_no")]
    public string PaymentNo { get; set; } = string.Empty;

    /// <summary>客户ID (mem_member.id)</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    /// <summary>实际收款金额（元）</summary>
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>收款方式：1-银行转账 2-支票 3-现金 4-其他</summary>
    [Column("payment_method")]
    public int PaymentMethod { get; set; } = 1;

    /// <summary>打款 / 到账日期</summary>
    [Column("payment_date", TypeName = "date")]
    public DateTime PaymentDate { get; set; }

    /// <summary>打款凭证 / 转账截图路径</summary>
    [StringLength(255)]
    [Column("voucher")]
    public string? Voucher { get; set; }

    /// <summary>备注（如：附言、用途）</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>后台操作人员ID（录入收款的管理员，sys_user.id）</summary>
    [Column("operator_id")]
    public int OperatorId { get; set; }

    /// <summary>状态：0-待核销 1-已核销</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>录入时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>核销明细集合（一笔收款拆分到多张应收单）</summary>
    public virtual List<MktPaymentItem> Items { get; set; } = new();
}
