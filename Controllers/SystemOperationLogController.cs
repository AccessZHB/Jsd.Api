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
/// 操作日志接口（系统管理模块）
/// 路由前缀：/api/system/operation-log
/// 删除 / 批量删除 / 清理仅超级管理员；查询登录用户均可。
/// </summary>
[ApiController]
[Route("api/system/operation-log")]
[Authorize]
public class SystemOperationLogController : ControllerBase
{
    private readonly ISystemLogService _logService;

    public SystemOperationLogController(ISystemLogService logService)
    {
        _logService = logService;
    }

    /// <summary>操作日志分页查询（操作人/模块/动作/状态/时间范围，create_time 倒序；权限：system:log:list）</summary>
    [HttpGet("list")]
    [Permission(SystemPermissions.OperationLogList)]
    public async Task<ApiResponse<PagedResult<OperationLogDto>>> GetList([FromQuery] OperationLogQueryDto query)
    {
        return await _logService.GetOperationLogsAsync(query);
    }

    /// <summary>操作日志详情（含请求参数、响应结果、异常堆栈；权限：system:log:list）</summary>
    [HttpGet("{id:long}")]
    [Permission(SystemPermissions.OperationLogList)]
    public async Task<ApiResponse<OperationLogDetailDto>> GetDetail(long id)
    {
        return await _logService.GetOperationLogDetailAsync(id);
    }

    /// <summary>删除单条操作日志（权限：system:log:remove，仅超管）</summary>
    [HttpDelete("{id:long}")]
    [Permission(SystemPermissions.OperationLogRemove)]
    [OperationLog("系统管理", "删除操作日志")]
    public async Task<ApiResponse<bool>> Delete(long id)
    {
        return await _logService.DeleteOperationLogAsync(id);
    }

    /// <summary>批量删除操作日志（权限：system:log:remove，仅超管）</summary>
    [HttpDelete("batch")]
    [Permission(SystemPermissions.OperationLogRemove)]
    [OperationLog("系统管理", "批量删除操作日志")]
    public async Task<ApiResponse<bool>> DeleteBatch([FromBody] BatchDeleteDto dto)
    {
        try
        {
            new BatchDeleteValidator().ValidateAndThrow(dto);
            return await _logService.DeleteOperationLogBatchAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>操作日志统计概览（今日总数 / 成功 / 失败 / 平均耗时；权限：system:log:list）</summary>
    [HttpGet("statistics")]
    [Permission(SystemPermissions.OperationLogList)]
    public async Task<ApiResponse<OperationLogStatisticsDto>> GetStatistics()
    {
        return await _logService.GetOperationLogStatisticsAsync();
    }

    /// <summary>清理过期操作日志（默认保留 90 天；权限：system:log:clean，仅超管）</summary>
    [HttpPost("clean")]
    [Permission(SystemPermissions.OperationLogClean)]
    [OperationLog("系统管理", "清理过期操作日志")]
    public async Task<ApiResponse<int>> Clean([FromBody] CleanLogDto? dto)
    {
        try
        {
            new CleanLogValidator().ValidateAndThrow(dto ?? new CleanLogDto());
            return await _logService.CleanOperationLogsAsync(dto ?? new CleanLogDto());
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<int>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>导出操作日志 Excel（权限：system:log:export，防止审计日志被批量外带）</summary>
    [HttpGet("export")]
    [Permission(SystemPermissions.OperationLogExport)]
    [OperationLog("系统管理", "导出操作日志")]
    public async Task<IActionResult> Export([FromQuery] OperationLogQueryDto query)
    {
        var (content, fileName) = await _logService.ExportOperationLogsAsync(query);
        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
