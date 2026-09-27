using FluentValidation;
using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Task;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 定时任务接口（自动任务模块，共 9 个）
///
/// 权限标识见 <see cref="TaskJobPermissions"/>：task:job:{list,add,edit,remove,start,stop,run,log}
///
/// 业务表 sys_job / sys_job_log（读写）+ Quartz 框架层 qrtz_* 表（由 Quartz 自己管理，业务代码不碰）。
/// </summary>
/// <remarks>
/// 路由顺序说明：GET /log 必须写在 GET /{id} 之前不生效（两者不冲突，因为 {id} 约束了 :long），
/// 但为了可读性仍把固定路径 /log 放在前面。
/// </remarks>
[ApiController]
[Route("api/task/job")]
[Authorize]
public class TaskJobController : ControllerBase
{
    private readonly IJobService _jobService;

    public TaskJobController(IJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>1. 任务列表（分页 + 名称/分组/状态筛选）</summary>
    /// <remarks>GET /api/task/job/list?jobName=&amp;jobGroup=&amp;status=1&amp;page=1&amp;pageSize=10</remarks>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<JobDto>>> GetList([FromQuery] JobQueryDto query)
        => await _jobService.GetPagedAsync(query);

    /// <summary>2. 任务详情</summary>
    /// <remarks>GET /api/task/job/1</remarks>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<JobDto?>> GetById(long id)
        => await _jobService.GetByIdAsync(id);

    /// <summary>9. 执行日志（分页 + 任务/状态/时间筛选）</summary>
    /// <remarks>GET /api/task/job/log?jobId=&amp;jobName=&amp;status=1&amp;fireTimeStart=&amp;fireTimeEnd=&amp;page=1&amp;pageSize=10</remarks>
    [HttpGet("log")]
    public async Task<ApiResponse<PagedResult<JobLogDto>>> GetLogList([FromQuery] JobLogQueryDto query)
    {
        try
        {
            new JobLogQueryValidator().ValidateAndThrow(query);
            return await _jobService.GetLogPagedAsync(query);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<PagedResult<JobLogDto>>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>3. 新增任务（Service 内二次校验 Cron 表达式 + job_type 类存在性 + 名称分组唯一）</summary>
    /// <remarks>POST /api/task/job</remarks>
    [HttpPost]
    [OperationLog("定时任务", "新增任务")]
    public async Task<ApiResponse<long>> Create([FromBody] CreateJobDto dto)
    {
        try
        {
            return await _jobService.CreateAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>4. 修改任务（Service 内先摘旧 Trigger 再重建）</summary>
    /// <remarks>PUT /api/task/job</remarks>
    [HttpPut]
    [OperationLog("定时任务", "修改任务")]
    public async Task<ApiResponse<bool>> Update([FromBody] UpdateJobDto dto)
    {
        try
        {
            return await _jobService.UpdateAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>5. 批量删除任务（先停 Quartz 再删库，ids 支持 "1,2,3"）</summary>
    /// <remarks>DELETE /api/task/job/1,2,3</remarks>
    [HttpDelete("{ids}")]
    [OperationLog("定时任务", "删除任务")]
    public async Task<ApiResponse<bool>> Delete(string ids)
    {
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();

        if (idList.Count == 0)
        {
            return ApiResponse<bool>.Fail("没有合法的待删除任务ID");
        }

        return await _jobService.DeleteAsync(string.Join(',', idList));
    }

    /// <summary>6. 启动任务（ResumeTrigger + 回写 status=1）</summary>
    /// <remarks>POST /api/task/job/start/1</remarks>
    [HttpPost("start/{id:long}")]
    [OperationLog("定时任务", "启动任务")]
    public async Task<ApiResponse<bool>> Start(long id)
        => await _jobService.StartAsync(id);

    /// <summary>7. 停止任务（PauseTrigger + 回写 status=0）</summary>
    /// <remarks>POST /api/task/job/stop/1</remarks>
    [HttpPost("stop/{id:long}")]
    [OperationLog("定时任务", "停止任务")]
    public async Task<ApiResponse<bool>> Stop(long id)
        => await _jobService.StopAsync(id);

    /// <summary>8. 立即执行一次（TriggerJob，幂等：已删除/未调度任务会直接拒绝）</summary>
    /// <remarks>POST /api/task/job/run/1</remarks>
    [HttpPost("run/{id:long}")]
    [OperationLog("定时任务", "立即执行任务")]
    public async Task<ApiResponse<JobRunResultDto>> Run(long id)
        => await _jobService.RunAsync(id);
}
