using Jsd.Api.Attributes;
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
/// 【安全说明】
///   1) 微信服务器回调不携带本系统 JWT，wechat-notify 保留 [AllowAnonymous]，
///      但必须通过微信平台证书 RSA-SHA256 验签 + APIv3 解密，验签失败绝不入账；
///   2) 会员自助创建充值单时，MemMemberId 强制取当前登录会员，严禁前端传入；
///   3) 列表/详情/代充/退款均需 [Permission] 或 [SuperAdminOnly]。
/// </summary>
[ApiController]
[Route("api/recharge")]
[Authorize]
public class RechargeController : ControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly CurrentUserService _currentUser;

    public RechargeController(IBalanceService balanceService, CurrentUserService currentUser)
    {
        _balanceService = balanceService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 创建充值订单，生成 recharge_no，返回支付参数。
    /// 【P0-2 修复】充值会员 ID 强制取当前登录会员，禁止前端指定他人 ID 越权充值。
    /// </summary>
    [HttpPost("create")]
    [Permission(RechargePermissions.RechargeCreate)]
    public async Task<ApiResponse<RechargePayParamsDto>> Create([FromBody] RechargeCreateDto dto)
    {
        try
        {
            // 会员令牌：强制使用当前登录会员 ID，覆盖前端传值，杜绝为他人创建充值单
            if (_currentUser.IsMember)
            {
                dto.MemMemberId = _currentUser.MemberId;
            }
            else if (dto.MemMemberId <= 0)
            {
                // 管理端代充应走 admin-create 接口；普通 create 未指定会员时拒绝
                return ApiResponse<RechargePayParamsDto>.Fail("请指定充值会员 ID", 400);
            }

            return await _balanceService.CreateRechargeAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<RechargePayParamsDto>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>分页查询充值记录（单号 / 客户 / 支付方式 / 状态 / 时间范围）</summary>
    [HttpGet("list")]
    [Permission(RechargePermissions.RechargeList)]
    public async Task<ApiResponse<PagedResult<RechargeDto>>> GetList([FromQuery] RechargeQueryDto query)
    {
        return await _balanceService.GetRechargesAsync(query);
    }

    /// <summary>充值单详情（含支付回调结果、微信交易号、会员当前余额）</summary>
    [HttpGet("{id:long}")]
    [Permission(RechargePermissions.RechargeDetail)]
    public async Task<ApiResponse<RechargeDto>> GetDetail(long id)
    {
        return await _balanceService.GetRechargeDetailAsync(id);
    }

    /// <summary>后台代客充值（线下收款，直接入账）</summary>
    [HttpPost("admin-create")]
    [SuperAdminOnly(Operation = "后台代客充值")]
    public async Task<ApiResponse<RechargeDto>> AdminCreate([FromBody] AdminRechargeDto dto)
    {
        return await _balanceService.AdminRechargeAsync(dto);
    }

    /// <summary>充值退款（仅已支付可退，需填退款原因）</summary>
    [HttpPost("{id:long}/refund")]
    [SuperAdminOnly(Operation = "充值退款")]
    public async Task<ApiResponse<bool>> Refund(long id, [FromBody] RechargeRefundDto dto)
    {
        return await _balanceService.RefundRechargeAsync(id, dto);
    }

    /// <summary>
    /// 【P0-1 修复】微信支付回调（V3）：
    ///   验签 → 解密 → 校验金额 → 幂等入账（transaction_id 唯一索引兜底）。
    /// 保留 [AllowAnonymous]（微信服务器无法携带本系统 JWT），但验签失败一律拒绝入账。
    /// </summary>
    [HttpPost("wechat-notify")]
    [AllowAnonymous]
    public async Task<ApiResponse<WechatNotifyResultDto>> WechatNotify()
    {
        var result = await _balanceService.HandleWechatNotifyRawAsync(HttpContext.Request);

        return result.Success
            ? ApiResponse<WechatNotifyResultDto>.Success(result, result.Message)
            : ApiResponse<WechatNotifyResultDto>.Fail(result.Message);
    }
}
