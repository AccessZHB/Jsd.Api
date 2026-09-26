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

    /// <summary>获取登录安全策略配置（失败锁定阈值/时长、密码强度、验证码开关）</summary>
    [HttpGet("config")]
    public async Task<ApiResponse<SecurityConfigDto>> GetConfig()
    {
        var config = await _securityService.GetConfigAsync();
        return ApiResponse<SecurityConfigDto>.Success(config);
    }

    /// <summary>修改登录安全策略配置（仅超管；写入 sys_config）</summary>
    [HttpPut("config")]
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
