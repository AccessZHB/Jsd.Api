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
/// 登录日志接口（系统管理模块）
/// 路由前缀：/api/system/login-log
/// 删除 / 批量删除 / 清理 / 解锁仅超级管理员；查询登录用户均可。
/// </summary>
[ApiController]
[Route("api/system/login-log")]
[Authorize]
public class SystemLoginLogController : ControllerBase
{
    private readonly ISystemLogService _logService;
    private readonly ISecurityService _securityService;

    public SystemLoginLogController(ISystemLogService logService, ISecurityService securityService)
    {
        _logService = logService;
        _securityService = securityService;
    }

    /// <summary>登录日志分页查询（用户名/IP/登录方式/状态/时间范围，create_time 倒序）</summary>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<LoginLogDto>>> GetList([FromQuery] LoginLogQueryDto query)
    {
        return await _logService.GetLoginLogsAsync(query);
    }

    /// <summary>登录日志详情</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<LoginLogDto>> GetDetail(long id)
    {
        return await _logService.GetLoginLogDetailAsync(id);
    }

    /// <summary>删除单条登录日志（仅超管）</summary>
    [HttpDelete("{id:long}")]
    [OperationLog("系统管理", "删除登录日志")]
    public async Task<ApiResponse<bool>> Delete(long id)
    {
        return await _logService.DeleteLoginLogAsync(id);
    }

    /// <summary>批量删除登录日志（仅超管）</summary>
    [HttpDelete("batch")]
    [OperationLog("系统管理", "批量删除登录日志")]
    public async Task<ApiResponse<bool>> DeleteBatch([FromBody] BatchDeleteDto dto)
    {
        try
        {
            new BatchDeleteValidator().ValidateAndThrow(dto);
            return await _logService.DeleteLoginLogBatchAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>清理过期登录日志（默认保留 180 天，返回删除条数；仅超管）</summary>
    [HttpPost("clean")]
    [OperationLog("系统管理", "清理过期登录日志")]
    public async Task<ApiResponse<int>> Clean([FromBody] CleanLogDto? dto)
    {
        try
        {
            new CleanLogValidator().ValidateAndThrow(dto ?? new CleanLogDto());
            return await _logService.CleanLoginLogsAsync(dto ?? new CleanLogDto());
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<int>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>导出登录日志 Excel（.xlsx，筛选条件同分页查询）</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] LoginLogQueryDto query)
    {
        var (content, fileName) = await _logService.ExportLoginLogsAsync(query);
        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    /// <summary>登录日志统计概览（今日总数 / 成功 / 失败 / 当前锁定账号数），前端页面顶部统计卡片</summary>
    [HttpGet("statistics")]
    public async Task<ApiResponse<LoginLogStatisticsDto>> GetStatistics()
    {
        return await _logService.GetLoginLogStatisticsAsync();
    }

    /// <summary>已锁定账号列表（当前仍处于锁定状态的账号，未解锁的）</summary>
    [HttpGet("lock/list")]
    public async Task<ApiResponse<PagedResult<LockAccountDto>>> GetLockList([FromQuery] LockAccountQueryDto query)
    {
        return await _logService.GetLockedAccountsAsync(query);
    }

    /// <summary>手动解锁账号（重置失败计数与锁定时间，写操作日志；仅超管）</summary>
    [HttpPost("unlock/{memberId:long}")]
    [OperationLog("系统管理", "手动解锁账号")]
    public async Task<ApiResponse<bool>> Unlock(long memberId)
    {
        return await _securityService.UnlockAsync(memberId);
    }
}
