using Jsd.Api.Attributes;
using FluentValidation;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 登录安全策略接口（系统管理模块）
/// 路由前缀：/api/system/security
/// 配置存 sys_config（config_key = security.login），修改仅超级管理员。
/// </summary>
[ApiController]
[Route("api/system/security")]
[Authorize]
public class SystemSecurityController : ControllerBase
{
    private readonly ISecurityService _securityService;

    public SystemSecurityController(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    /// <summary>
    /// 获取完整登录安全策略配置（失败锁定阈值/时长、密码强度、验证码开关、Token 有效期等）。
    /// 【P0-13 修复】原先整接口 [AllowAnonymous]，等于把「锁定前可试几次 / 验证码是否开启 /
    /// 密码复杂度下限」这些撞库所需情报直接公开。现改为必须登录 + 权限校验。
    /// 登录页只需要"是否开启验证码 + 验证码长度"，请改调 /api/system/security/captcha-policy。
    /// </summary>
    [HttpGet("config")]
    [Permission(SystemPermissions.SecurityList)]
    public async Task<ApiResponse<SecurityConfigDto>> GetConfig()
    {
        var config = await _securityService.GetConfigAsync();
        return ApiResponse<SecurityConfigDto>.Success(config);
    }

    /// <summary>
    /// 登录页专用：仅披露验证码开关与长度（最小必要信息）。
    /// 保持匿名（登录页未登录就要判断是否展示验证码），但**不返回锁定阈值、密码策略、Token 时长**。
    /// </summary>
    [HttpGet("captcha-policy")]
    [AllowAnonymous]
    public async Task<ApiResponse<object>> GetCaptchaPolicy()
    {
        var config = await _securityService.GetConfigAsync();
        return ApiResponse<object>.Success(new
        {
            captchaEnabled = config.CaptchaEnabled,
            captchaLength = config.CaptchaLength
        });
    }

    /// <summary>修改登录安全策略配置（权限：system:security:update，仅超管；写 sys_config）</summary>
    [HttpPut("config")]
    [Permission(SystemPermissions.SecurityUpdate)]
    [OperationLog("系统管理", "修改登录安全策略")]
    public async Task<ApiResponse<bool>> UpdateConfig([FromBody] SecurityConfigDto dto)
    {
        try
        {
            new SecurityConfigValidator().ValidateAndThrow(dto);
            return await _securityService.UpdateConfigAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }
}
