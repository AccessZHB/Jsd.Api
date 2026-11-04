using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Models.Wallet;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 会员钱包（余额 / 充值）
/// 路由前缀：api/wallet
///
/// 【阶段六 · 资损加固】自助充值当前是"模拟支付"：未对接微信支付，调用即直接入账。
/// 这意味着 **充值金额完全由客户端决定** —— 会员传 amount=999999 就能凭空得到余额再下单。
/// 因此增加两道闸门（配置在 appsettings: Wallet:SelfRecharge）：
///   1. Enabled=false（默认）：直接拒绝，提示联系客服；接入微信支付后由支付回调入账再打开；
///   2. MaxAmount：单笔上限，防止误填/恶意大额。
/// 演示环境可临时把 Enabled 置 true。
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly IConfiguration _configuration;

    public WalletController(IBalanceService balanceService, IConfiguration configuration)
    {
        _balanceService = balanceService;
        _configuration = configuration;
    }

    /// <summary>
    /// 会员自助充值。
    ///
    /// 【阶段六】资损加固：金额由客户端传入且"调用即入账"，等于给会员开了一台印钞机。
    /// 现加两道闸门（appsettings: Wallet:SelfRecharge）：
    ///   Enabled   —— 默认 false，直接拒绝（接入微信支付后，由支付回调入账再打开）；
    ///   MaxAmount —— 单笔上限，默认 5000 元。
    /// </summary>
    [HttpPost("recharge")]
    [ProducesResponseType(typeof(ApiResponse<RechargeDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<RechargeDto>> Recharge([FromBody] WalletRechargeDto dto)
    {
        var section = _configuration.GetSection("Wallet:SelfRecharge");
        var enabled = section.GetValue("Enabled", false);
        if (!enabled)
        {
            // 未接支付渠道时绝不允许"凭接口调用增加余额"
            return ApiResponse<RechargeDto>.Fail("在线充值暂未开通，请联系客服线下充值", 403);
        }

        var maxAmount = section.GetValue("MaxAmount", 5000m);
        if (dto.Amount <= 0)
        {
            return ApiResponse<RechargeDto>.Fail("充值金额必须大于 0");
        }
        if (maxAmount > 0 && dto.Amount > maxAmount)
        {
            return ApiResponse<RechargeDto>.Fail($"单笔充值金额不能超过 {maxAmount:0.##} 元");
        }

        try
        {
            return await _balanceService.SelfRechargeAsync(dto.Amount);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<RechargeDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 会员钱包流水分页（小程序「充值记录 / 消费记录」）。
    /// type=recharge 充值；type=consume 消费（下单扣减/下单冻结）。
    /// ⚠️ 会员身份从 Token（member_id）解析，强制按 mem_member_id 归属过滤，只能看自己的账单。
    /// </summary>
    [HttpGet("record/list")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletRecordItemDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<WalletRecordItemDto>>> GetRecordList(
        [FromQuery] string type = "recharge",
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        return await _balanceService.GetMyRecordsAsync(type, page, size);
    }
}
