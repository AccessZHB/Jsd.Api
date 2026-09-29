using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 对账差异核查表 reconciliation_check（T+1 对账模块）
///
/// 一条记录 = 一处「账账不符」，供财务人工核查与核销。
/// ⚠️ 重跑对账（同一账单日）会先物理清除该日的旧差异再重新写入，
///    保证幂等 —— 否则任务重 跑一次差异就翻倍。
/// </summary>
[Table("reconciliation_check")]
public class ReconciliationCheck
{
    /// <summary>ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联对账单主表ID（wechat_daily_bill.id）</summary>
    [Column("bill_id")]
    public long BillId { get; set; }

    /// <summary>关联订单ID（payment_order.id；长款-仅微信有时为 0）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>商户订单号</summary>
    [StringLength(64)]
    [Column("out_trade_no")]
    public string? OutTradeNo { get; set; }

    /// <summary>微信交易号</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>差异类型（见 DiffType：1长款 2短款 3金额不符 4状态不符）</summary>
    [Column("diff_type")]
    public int DiffType { get; set; }

    /// <summary>微信侧金额（单位：元；短款时系统侧有、微信无，填 0）</summary>
    [Column("wechat_amount")]
    public decimal WechatAmount { get; set; }

    /// <summary>系统侧金额（单位：元；长款时微信有、系统无，填 0）</summary>
    [Column("system_amount")]
    public decimal SystemAmount { get; set; }

    /// <summary>微信侧交易状态</summary>
    [StringLength(32)]
    [Column("wechat_status")]
    public string? WechatStatus { get; set; }

    /// <summary>系统侧支付状态（PayOrderStatus）</summary>
    [Column("system_status")]
    public int? SystemStatus { get; set; }

    /// <summary>处理状态（见 CheckHandleStatus：0待处理 1已处理 2挂账）</summary>
    [Column("handle_status")]
    public int HandleStatus { get; set; }

    /// <summary>处理人</summary>
    [StringLength(64)]
    [Column("handler")]
    public string? Handler { get; set; }

    /// <summary>处理备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>核查时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
