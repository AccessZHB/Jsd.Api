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
