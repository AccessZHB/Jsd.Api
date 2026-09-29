using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 微信对账单明细表 wechat_bill_detail（T+1 对账模块）
///
/// 一条记录 = 微信账单 CSV 的一行（支付/退款/关闭等各种交易状态都会落记录）。
/// ⚠️ 金额单位统一为【元】（DECIMAL(18,2)），与微信账单原始列一致；
///    系统侧 payment_order.amount 是【分】，比对时统一换算成「元」再比较，避免量纲错误。
/// </summary>
[Table("wechat_bill_detail")]
public class WechatBillDetail
{
    /// <summary>ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联主表ID（wechat_daily_bill.id）</summary>
    [Column("bill_id")]
    public long BillId { get; set; }

    /// <summary>微信交易号</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>商户订单号（与系统 payment_order.out_trade_no 连接的核心键）</summary>
    [StringLength(64)]
    [Column("out_trade_no")]
    public string? OutTradeNo { get; set; }

    /// <summary>交易时间（账单「交易时间」列）</summary>
    [Column("trade_time")]
    public DateTime? TradeTime { get; set; }

    /// <summary>交易状态（SUCCESS / REFUND / REVOKED / NOTPAY / CLOSED / PAYERROR）</summary>
    [StringLength(32)]
    [Column("trade_state")]
    public string? TradeState { get; set; }

    /// <summary>订单金额（单位：元）</summary>
    [Column("total_fee")]
    public decimal TotalFee { get; set; }

    /// <summary>退款金额（单位：元）</summary>
    [Column("refund_fee")]
    public decimal RefundFee { get; set; }

    /// <summary>手续费（单位：元）</summary>
    [Column("poundage")]
    public decimal Poundage { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
