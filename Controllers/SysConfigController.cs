using FluentValidation;
using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 系统配置接口（系统管理模块，共 9 个）
///
/// 权限标识见 <see cref="SysConfigPermissions"/>：
/// system:config:list / create / update / delete / cache（按钮级，由 sys_menu.permission 下发）
///
/// 操作日志：写操作打 [OperationLog]，由 OperationLogFilter 异步采集落库，Controller 内不写日志代码。
/// </summary>
[ApiController]
[Route("api/system/config")]
[Authorize]
public class SysConfigController : ControllerBase
{
    private readonly ISysConfigService _configService;

    public SysConfigController(ISysConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>配置列表（分页 + 条件搜索）</summary>
    /// <remarks>GET /api/system/config/list?configName=&amp;configKey=&amp;configType=&amp;status=0&amp;page=1&amp;pageSize=10</remarks>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<SysConfigDto>>> GetList([FromQuery] SysConfigQueryDto query)
        => await _configService.GetPagedListAsync(query);

    /// <summary>配置详情</summary>
    /// <remarks>GET /api/system/config/1</remarks>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<SysConfigDetailDto?>> GetById(long id)
        => await _configService.GetByIdAsync(id);

    /// <summary>新增配置（校验 config_key 唯一）</summary>
    /// <remarks>POST /api/system/config</remarks>
    [HttpPost]
    [OperationLog("系统管理", "新增系统配置")]
    public async Task<ApiResponse<long>> Create([FromBody] CreateSysConfigDto dto)
    {
        try
        {
            return await _configService.CreateAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>修改配置（系统内置仅允许改配置值）</summary>
    /// <remarks>PUT /api/system/config</remarks>
    [HttpPut]
    [OperationLog("系统管理", "修改系统配置")]
    public async Task<ApiResponse<bool>> Update([FromBody] UpdateSysConfigDto dto)
    {
        try
        {
            return await _configService.UpdateAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>批量删除配置（逗号分隔；系统内置 config_type='Y' 不可删除）</summary>
    /// <remarks>DELETE /api/system/config/1,2,3</remarks>
    [HttpDelete("{ids}")]
    [OperationLog("系统管理", "删除系统配置")]
    public async Task<ApiResponse<int>> Delete(string ids)
    {
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();

        return await _configService.DeleteAsync(idList);
    }

    /// <summary>按 key 获取配置值（优先读缓存）</summary>
    /// <remarks>GET /api/system/config/value/security.login</remarks>
    [HttpGet("value/{configKey}")]
    public async Task<ApiResponse<ConfigValueDto>> GetValue(string configKey)
        => await _configService.GetValueAsync(configKey);

    /// <summary>按 key 列表批量获取配置值（一次回源，不循环打库）</summary>
    /// <remarks>POST /api/system/config/values  body: { "keys": ["sys.app.name", "security.login"] }</remarks>
    [HttpPost("values")]
    public async Task<ApiResponse<List<ConfigValueDto>>> GetValues([FromBody] ConfigKeysDto dto)
    {
        try
        {
            new ConfigKeysValidator().ValidateAndThrow(dto);
            return await _configService.GetValuesAsync(dto.Keys);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<List<ConfigValueDto>>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>刷新配置缓存（清空全部并重新从数据库装载）</summary>
    /// <remarks>POST /api/system/config/cache/refresh</remarks>
    [HttpPost("cache/refresh")]
    [OperationLog("系统管理", "刷新配置缓存")]
    public async Task<ApiResponse<ConfigCacheResultDto>> RefreshCache()
        => await _configService.RefreshCacheAsync();

    /// <summary>清空配置缓存</summary>
    /// <remarks>POST /api/system/config/cache/clear</remarks>
    [HttpPost("cache/clear")]
    [OperationLog("系统管理", "清空配置缓存")]
    public async Task<ApiResponse<ConfigCacheResultDto>> ClearCache()
        => await _configService.ClearCacheAsync();
}
