using Jsd.Api.Models.Common;
using Jsd.Api.Models.Task;

namespace Jsd.Api.Services;

/// <summary>
/// 定时任务服务接口（自动任务模块，9 个接口）
/// 业务表 sys_job / sys_job_log + Quartz 框架层联动（启停走 Resume/PauseTrigger，删除走 DeleteJob）。
/// </summary>
public interface IJobService
{
    /// <summary>任务列表（分页 + 名称/分组/状态筛选）</summary>
    Task<ApiResponse<PagedResult<JobDto>>> GetPagedAsync(JobQueryDto query);

    /// <summary>任务详情</summary>
    Task<ApiResponse<JobDto?>> GetByIdAsync(long id);

    /// <summary>新增任务（校验 Cron 表达式 + 反射类存在性 + 名称分组唯一）</summary>
    Task<ApiResponse<long>> CreateAsync(CreateJobDto dto);

    /// <summary>修改任务（校验通过后重建 Quartz Trigger）</summary>
    Task<ApiResponse<bool>> UpdateAsync(UpdateJobDto dto);

    /// <summary>批量删除（ids 支持 "1,2,3"；先摘 Quartz 再删库）</summary>
    Task<ApiResponse<bool>> DeleteAsync(string ids);

    /// <summary>启动任务（ResumeTrigger + 回写 status=1）</summary>
    Task<ApiResponse<bool>> StartAsync(long id);

    /// <summary>停止任务（PauseTrigger + 回写 status=0）</summary>
    Task<ApiResponse<bool>> StopAsync(long id);

    /// <summary>立即执行一次（TriggerJob）</summary>
    Task<ApiResponse<JobRunResultDto>> RunAsync(long id);

    /// <summary>执行日志列表（分页 + 任务/状态/时间筛选）</summary>
    Task<ApiResponse<PagedResult<JobLogDto>>> GetLogPagedAsync(JobLogQueryDto query);
}
