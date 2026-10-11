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
/// 说明：当前未对接微信支付，自助充值接口调用即直接入账（模拟支付）。
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IBalanceService _balanceService;

    public WalletController(IBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// 会员自助充值：未对接微信支付，调用即直接入账（模拟支付）。
    /// 事务内完成：建充值单（已到账）+ 余额累加 + 累计充值累加 + 写充值流水。
    /// </summary>
    [HttpPost("recharge")]
    [ProducesResponseType(typeof(ApiResponse<RechargeDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<RechargeDto>> Recharge([FromBody] WalletRechargeDto dto)
    {
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
