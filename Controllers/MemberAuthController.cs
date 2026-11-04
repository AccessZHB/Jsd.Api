using Jsd.Api.Attributes;
using Jsd.Api.Exceptions;
using Jsd.Api.Helpers;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;
using Jsd.Api.Models.System;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 小程序会员（买家）自助接口：验证码 + 登录 + 登出 + 当前会员资料。
///
/// 与后台管理员体系（api/auth、sys_user）完全隔离：
///   管理员用 AuthController 登录，令牌带 is_super / role_id；
///   会员用本控制器登录，令牌带 user_type=member / member_id。
/// 两套账号体系物理分表（sys_user vs mem_member），令牌互不通用。
/// </summary>
[ApiController]
[Route("api/member")]
public class MemberAuthController : ControllerBase
{
    private readonly IMemMemberService _memberService;
    private readonly ISecurityService _securityService;
    private readonly ITokenBlacklistService _tokenBlacklist;

    public MemberAuthController(
        IMemMemberService memberService,
        ISecurityService securityService,
        ITokenBlacklistService tokenBlacklist)
    {
        _memberService = memberService;
        _securityService = securityService;
        _tokenBlacklist = tokenBlacklist;
    }

    /// <summary>
    /// 【F-25】获取登录图形验证码。
    /// 返回：{ captchaKey（登录时原样回传）, captchaImage（data URI，img 标签直接用）}
    /// 有效期 5 分钟，且**一次性**（校验后立即作废）。
    /// 示例：POST /api/member/captcha
    /// </summary>
    [HttpPost("captcha")]
    [AllowAnonymous]
    public async Task<ApiResponse<CaptchaResultDto>> Captcha()
    {
        return await _securityService.GenerateCaptchaAsync();
    }

    /// <summary>
    /// 小程序会员登录。
    /// 请求体：{ "userName":"shengdai01", "password":"123456", "captchaKey":"...", "captchaCode":"A1B2" }
    /// 安全策略 Security:MemberLogin:CaptchaEnabled=true 时，captchaKey/captchaCode 必传，
    /// 且验证码校验失败会**直接返回、不进入密码比对**（防撞库）。
    /// 连续失败 MaxLoginAttempts 次后锁定 LockDurationMinutes 分钟。
    /// 成功返回：token / expiresIn / memberId / memberNo / name / levelName / balance
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]   // 登录接口不需要 Token
    [ReplayAttack]     // 阶段五：防重放（X-Nonce / X-Timestamp，±60s，Nonce 记忆 2 分钟）
    public async Task<ApiResponse<MemberLoginResultDto>> Login([FromBody] MemberLoginDto dto)
    {
        try
        {
            return await _memberService.LoginAsync(dto);
        }
        catch (BusinessException ex)
        {
            // 阶段三：账号处于登录锁定中 —— 业务异常转成统一响应，绝不冒泡成 500
            return ApiResponse<MemberLoginResultDto>.Fail(ex.Message, 403);
        }
        catch (InvalidOperationException ex)
        {
            // 与全站约定一致：可预期的业务拒绝一律 4xx，不抛 500
            return ApiResponse<MemberLoginResultDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 【F-22】会员登出：把当前 Token 加入黑名单，立即失效（而非仅仅前端删除本地缓存）。
    /// 请求头：Authorization: Bearer {token}
    /// 示例：POST /api/member/logout
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResponse<object>> Logout()
    {
        var token = BearerTokenHelper.ReadToken(HttpContext);
        if (string.IsNullOrEmpty(token))
        {
            return ApiResponse<object>.Fail("未获取到登录令牌", 401);
        }

        var expiresAt = BearerTokenHelper.ReadExpiry(token);
        if (expiresAt is null)
        {
            return ApiResponse<object>.Fail("登录令牌无效", 401);
        }

        await _tokenBlacklist.RevokeAsync(token, expiresAt.Value);
        return ApiResponse<object>.Success(new { }, "已退出登录，令牌已失效");
    }

    /// <summary>
    /// 当前登录会员资料（小程序「我的」页）
    /// 请求头：Authorization: Bearer {会员登录返回的 token}
    /// 会员ID 从 JWT 的 member_id 声明读取；管理员令牌无此声明，返回 401。
    /// 示例：GET /api/member/profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ApiResponse<MemberProfileDto>> Profile()
    {
        // 会员令牌独有 member_id 声明；管理员令牌没有 → 视为非会员
        var raw = User.FindFirst("member_id")?.Value;
        if (!long.TryParse(raw, out var memberId) || memberId <= 0)
        {
            return ApiResponse<MemberProfileDto>.Fail("未登录或不是会员令牌", 401);
        }

        return await _memberService.GetProfileAsync(memberId);
    }
}
