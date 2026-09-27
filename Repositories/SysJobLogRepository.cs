using Jsd.Api.Entities;
using Jsd.Api.Models.Task;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>任务执行日志仓储实现（sys_job_log）</summary>
public class SysJobLogRepository : Repository<SysJobLog>, ISysJobLogRepository
{
    public SysJobLogRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<(List<SysJobLog> Items, int Total)> GetPagedAsync(JobLogQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 200);

        var q = Db.SysJobLogs.AsNoTracking().AsQueryable();

        if (query.JobId.HasValue && query.JobId.Value > 0)
        {
            q = q.Where(x => x.JobId == query.JobId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.JobName))
        {
            var name = query.JobName.Trim();
            q = q.Where(x => EF.Functions.Like(x.JobName, $"%{name}%"));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(x => x.Status == query.Status.Value);
        }

        if (query.FireTimeStart.HasValue)
        {
            q = q.Where(x => x.FireTime >= query.FireTimeStart.Value);
        }

        if (query.FireTimeEnd.HasValue)
        {
            // 覆盖到当天 23:59:59，避免"止于当天 00:00"导致当天日志查不到
            var end = query.FireTimeEnd.Value.Date.AddDays(1).AddSeconds(-1);
            q = q.Where(x => x.FireTime <= end);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(x => x.FireTime)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
