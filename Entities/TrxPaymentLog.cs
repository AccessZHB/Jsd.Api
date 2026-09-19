using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 支付日志表 trx_payment_log（微信支付回调原始数据，技术对账基石）
///
/// 【业务定位】
///   记录微信支付回调的原始数据，用于支付对账、退款追踪、异常排查。
///   与 mkt_payment（线下人工收款）互补：本表是"线上支付"的资金流水记录。
///
/// 【写入时机】
///   由 PaymentCallbackController 接收微信回调时写入；
///   仅做数据记录，无独立菜单，也不承载复杂业务逻辑。
/// </summary>
[Table("trx_payment_log")]
public class TrxPaymentLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联订单ID (trx_order.id)</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>订单号（冗余，便于查询）</summary>
    [Required]
    [StringLength(32)]
    [Column("order_no")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>微信支付交易号 (transaction_id)</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>商户订单号 (out_trade_no)</summary>
    [StringLength(64)]
    [Column("out_trade_no")]
    public string? OutTradeNo { get; set; }

    /// <summary>支付方式：1-微信支付(JSAPI) 2-余额支付 3-其他</summary>
    [Column("payment_type")]
    public int PaymentType { get; set; } = 1;

    /// <summary>支付金额（元）</summary>
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>支付状态：0-待支付 1-已支付 2-已取消 3-已退款 4-支付失败</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>微信回调原始数据（XML/JSON，用于对账）</summary>
    [Column("callback_data")]
    public string? CallbackData { get; set; }

    /// <summary>微信回调时间</summary>
    [Column("callback_time")]
    public DateTime? CallbackTime { get; set; }

    /// <summary>错误码（支付失败时记录）</summary>
    [StringLength(64)]
    [Column("error_code")]
    public string? ErrorCode { get; set; }

    /// <summary>错误描述</summary>
    [StringLength(500)]
    [Column("error_msg")]
    public string? ErrorMsg { get; set; }

    /// <summary>操作人员ID（后台管理员；回调场景一般为空）</summary>
    [Column("operator_id")]
    public int? OperatorId { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
