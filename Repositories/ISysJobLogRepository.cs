using Jsd.Api.Entities;
using Jsd.Api.Models.Task;

namespace Jsd.Api.Repositories;

/// <summary>任务执行日志仓储接口（自动任务模块，sys_job_log）</summary>
public interface ISysJobLogRepository
{
    /// <summary>分页查询执行日志（支持任务ID / 任务名 / 状态 / 触发时间范围筛选）</summary>
    Task<(List<SysJobLog> Items, int Total)> GetPagedAsync(JobLogQueryDto query);
}
