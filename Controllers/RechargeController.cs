using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 会员充值接口（价格策略与会员余额模块）
/// 路由前缀：/api/recharge
///
/// 【先充值后下单】本接口是客户资金的唯一入口：
///   创建充值单（待支付） → 微信支付 → 回调入账（balance + gift） → 客户才能下单。
///
/// 【安全说明】微信服务器回调不携带本系统 JWT，故 wechat-notify 标记 [AllowAnonymous]。
///   生产环境必须接入真实验签（微信平台证书），防止伪造回调入账。
/// </summary>
[ApiController]
[Route("api/recharge")]
[Authorize]
public class RechargeController : ControllerBase
{
    private readonly IBalanceService _balanceService;

    public RechargeController(IBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>创建充值订单，生成 recharge_no，返回支付参数</summary>
    [HttpPost("create")]
    public async Task<ApiResponse<RechargePayParamsDto>> Create([FromBody] RechargeCreateDto dto)
    {
        try
        {
            return await _balanceService.CreateRechargeAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<RechargePayParamsDto>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>分页查询充值记录（单号 / 客户 / 支付方式 / 状态 / 时间范围）</summary>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<RechargeDto>>> GetList([FromQuery] RechargeQueryDto query)
    {
        return await _balanceService.GetRechargesAsync(query);
    }

    /// <summary>充值单详情（含支付回调结果、微信交易号、会员当前余额）</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<RechargeDto>> GetDetail(long id)
    {
        return await _balanceService.GetRechargeDetailAsync(id);
    }

    /// <summary>后台代客充值（线下收款，直接入账）</summary>
    [HttpPost("admin-create")]
    public async Task<ApiResponse<RechargeDto>> AdminCreate([FromBody] AdminRechargeDto dto)
    {
        return await _balanceService.AdminRechargeAsync(dto);
    }

    /// <summary>充值退款（仅已支付可退，需填退款原因）</summary>
    [HttpPost("{id:long}/refund")]
    public async Task<ApiResponse<bool>> Refund(long id, [FromBody] RechargeRefundDto dto)
    {
        return await _balanceService.RefundRechargeAsync(id, dto);
    }

    /// <summary>
    /// 微信支付回调：基于 transaction_id 幂等校验，事务内更新订单状态 → 累加余额 → 写余额流水。
    /// 返回微信要求的应答结构（本项目统一为 ApiResponse 包装的 code/message）。
    /// </summary>
    [HttpPost("wechat-notify")]
    [AllowAnonymous]
    public async Task<ApiResponse<WechatNotifyResultDto>> WechatNotify([FromBody] WechatNotifyDto dto)
    {
        var result = await _balanceService.HandleWechatNotifyAsync(dto);

        return result.Success
            ? ApiResponse<WechatNotifyResultDto>.Success(result, result.Message)
            : ApiResponse<WechatNotifyResultDto>.Fail(result.Message);
    }
}
