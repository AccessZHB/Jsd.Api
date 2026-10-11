using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Wallet;

/// <summary>
/// 会员自助充值入参（POST /api/wallet/recharge）
/// 未对接微信支付，调用即视为"已支付"直接入账（模拟支付）。
/// </summary>
public class WalletRechargeDto
{
    /// <summary>充值金额（元，decimal(10,2)）；必须大于 0</summary>
    [Required]
    public decimal Amount { get; set; }
}

/// <summary>
/// 会员钱包流水项（GET /api/wallet/record/list，小程序「充值记录 / 消费记录」）。
/// 数据源：mkt_balance_log（按当前会员 mem_member_id 强制过滤）。
/// </summary>
public class WalletRecordItemDto
{
    /// <summary>流水ID</summary>
    public long Id { get; set; }

    /// <summary>变动类型（BalanceChangeType）：1充值 2下单扣减 3退款退回 5后台调整 7冻结 8解冻</summary>
    public int ChangeType { get; set; }

    /// <summary>变动类型文案（如「充值」「下单扣减」）</summary>
    public string ChangeTypeName { get; set; } = string.Empty;

    /// <summary>变动金额（带符号：收入为正、支出为负，前端直接展示 +/-）</summary>
    public decimal Amount { get; set; }

    /// <summary>变动后余额</summary>
    public decimal AfterBalance { get; set; }

    /// <summary>关联单据类型（BalanceRelatedType）：0无 1订单 2充值订单 3退款单</summary>
    public int? RelatedType { get; set; }

    /// <summary>关联单号（消费=订单号 trx_order.order_no；充值=充值单号 mem_recharge.recharge_no）</summary>
    public string? RelatedOrderNo { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>变动时间（yyyy-MM-dd HH:mm:ss）</summary>
    public string CreateTime { get; set; } = string.Empty;
}
