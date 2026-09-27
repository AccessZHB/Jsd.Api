using Jsd.Api.Entities;
using Jsd.Api.Models.Task;

namespace Jsd.Api.Repositories;

/// <summary>定时任务定义仓储接口（自动任务模块）</summary>
public interface ISysJobRepository : IRepository<SysJob>
{
    /// <summary>分页查询任务列表（支持名称模糊 / 分组精确 / 状态筛选）</summary>
    Task<(List<SysJob> Items, int Total)> GetPagedAsync(JobQueryDto query);

    /// <summary>判断「同一分组下任务名」是否已存在（excludeId 用于编辑时排除自身）</summary>
    Task<bool> ExistsNameAsync(string jobName, string jobGroup, long excludeId);

    /// <summary>按「任务名 + 分组」查任务（Quartz JobKey 落日志时反查 sys_job.id）</summary>
    Task<SysJob?> FindByNameAndGroupAsync(string jobName, string jobGroup);

    /// <summary>批量删除（按 ID 集合）</summary>
    Task<int> DeleteAsync(List<long> ids);
}
