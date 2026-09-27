using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 支付单表 payment_order（微信支付主动查单与补单模块）
///
/// 生命周期：INIT(0) → PAYING(1) → SUCCESS(2) / FAIL(3) / CLOSED(4)。
///   - 业务系统发起支付时插入一条 PAYING 记录，并拿 out_trade_no 去微信下单；
///   - 微信回调 / 定时查单命中 SUCCESS → 本地更新为 SUCCESS（并回填 transaction_id）；
///   - 超时未支付（now &gt; expire_time）且微信侧也未支付 → 定时任务关单 CLOSED。
/// ⚠️ out_trade_no 唯一索引是「拦截重复支付请求」的最后一道防线（重复 out_trade_no 直接 DB 报错）。
/// </summary>
[Table("payment_order")]
public class PaymentOrder
{
    /// <summary>支付单ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联业务订单ID（如 trx_order.id，用于支付成功后推进业务订单）</summary>
    [Column("biz_order_id")]
    public long BizOrderId { get; set; }

    /// <summary>商户订单号（业务系统生成，唯一，拦截重复请求）</summary>
    [Required]
    [StringLength(64)]
    [Column("out_trade_no")]
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信交易号（支付成功回调解密后回填；未支付为空）</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>支付金额（单位：分，与微信 amount.total 同一量纲，避免浮点误差）</summary>
    [Column("amount")]
    public long Amount { get; set; }

    /// <summary>支付渠道（见 PayChannel：1-JSAPI 2-APP 3-Native 4-H5）</summary>
    [Column("pay_channel")]
    public int PayChannel { get; set; }

    /// <summary>当前支付状态（见 PayOrderStatus：0-INIT 1-PAYING 2-SUCCESS 3-FAIL 4-CLOSED）</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>关单/超时时间（超过后定时任务关单）</summary>
    [Column("expire_time")]
    public DateTime ExpireTime { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
