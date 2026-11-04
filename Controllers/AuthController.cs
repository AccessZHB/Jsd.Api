using Jsd.Api.Attributes;
using FluentValidation;
using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 认证接口（登录 / 登出 / 验证码 / Token 刷新 / 修改密码）
/// 系统管理模块扩展：图形验证码、双令牌刷新、改密强制下线。
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ISecurityService _securityService;
    private readonly ITokenBlacklistService _tokenBlacklist;

    public AuthController(
        IAuthService authService,
        ISecurityService securityService,
        ITokenBlacklistService tokenBlacklist)
    {
        _authService = authService;
        _securityService = securityService;
        _tokenBlacklist = tokenBlacklist;
    }

    /// <summary>
    /// 管理员登录
    /// 请求体：{ "userName": "admin", "password": "admin123", "captchaKey": "...", "captchaCode": "A1B2" }
    /// 验证码从 POST /api/auth/captcha 获取；安全策略开启验证码时 captchaKey/captchaCode 必传。
    /// 成功返回：accessToken / refreshToken / expiresIn + 用户基本信息（token 字段为兼容旧前端保留）
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]  // 登录接口不需要 Token
    [ReplayAttack]    // 阶段五：防重放（X-Nonce / X-Timestamp，±60s，Nonce 记忆 2 分钟）
    public async Task<ApiResponse<LoginResultDto>> Login([FromBody] LoginDto dto)
    {
        // 获取客户端IP与UA，用于登录日志与最后登录记录
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        return await _authService.LoginAsync(dto, clientIp, string.IsNullOrEmpty(userAgent) ? null : userAgent);
    }

    /// <summary>
    /// 生成图形验证码（SVG Base64）
    /// 返回：{ captchaKey（登录回传）, captchaImage（data URI，img 标签直接可用）}
    /// TTL 5 分钟，一次性消耗。
    /// </summary>
    [HttpPost("captcha")]
    [AllowAnonymous]
    public async Task<ApiResponse<CaptchaResultDto>> Captcha()
    {
        return await _securityService.GenerateCaptchaAsync();
    }

    /// <summary>
    /// JWT Token 刷新（旧 refreshToken 一次性核销防重放；过期则要求重新登录）
    /// 请求体：{ "refreshToken": "..." }
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ApiResponse<TokenResultDto>> Refresh([FromBody] RefreshTokenDto dto)
    {
        return await _securityService.RefreshTokenAsync(dto);
    }

    /// <summary>
    /// 修改密码（校验旧密码 + 强度；成功后旧 JWT 全部失效，强制重新登录）
    /// 请求体：{ "oldPassword": "...", "newPassword": "...", "confirmPassword": "..." }
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [OperationLog("系统管理", "修改密码")]
    public async Task<ApiResponse<bool>> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        try
        {
            new ChangePasswordValidator().ValidateAndThrow(dto);
            return await _securityService.ChangePasswordAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>
    /// 登出（F-22）：把当前请求头里的 Token 加入黑名单，立即失效。
    ///
    /// 说明：JWT 无状态，仅靠前端删除本地 Token 无法阻止他人复用；
    /// 这里按 Token 的**剩余有效期**写入黑名单（Redis / 内存），
    /// 由 TokenBlacklistMiddleware 在每个请求上拦截。
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResponse<object>> Logout()
    {
        var token = Helpers.BearerTokenHelper.ReadToken(HttpContext);
        if (string.IsNullOrEmpty(token))
        {
            // 理论上 [Authorize] 已保证有 Token；这里只做防御性返回，不抛 500
            return ApiResponse<object>.Fail("未获取到登录令牌", 401);
        }

        var expiresAt = Helpers.BearerTokenHelper.ReadExpiry(token);
        if (expiresAt is null)
        {
            return ApiResponse<object>.Fail("登录令牌无效", 401);
        }

        await _tokenBlacklist.RevokeAsync(token, expiresAt.Value);
        return ApiResponse<object>.Success(new { }, "登出成功，令牌已失效");
    }
}
