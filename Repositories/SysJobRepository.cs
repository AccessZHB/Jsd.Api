using Jsd.Api.Entities;
using Jsd.Api.Models.Task;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>定时任务定义仓储实现（sys_job）</summary>
public class SysJobRepository : Repository<SysJob>, ISysJobRepository
{
    public SysJobRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<(List<SysJob> Items, int Total)> GetPagedAsync(JobQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 200);

        var q = Db.SysJobs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.JobName))
        {
            var name = query.JobName.Trim();
            q = q.Where(x => EF.Functions.Like(x.JobName, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.JobGroup))
        {
            var group = query.JobGroup.Trim();
            q = q.Where(x => x.JobGroup == group);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(x => x.Status == query.Status.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsNameAsync(string jobName, string jobGroup, long excludeId)
    {
        return await Db.SysJobs
            .AsNoTracking()
            .AnyAsync(x => x.JobName == jobName && x.JobGroup == jobGroup && x.Id != excludeId);
    }

    /// <inheritdoc/>
    public async Task<SysJob?> FindByNameAndGroupAsync(string jobName, string jobGroup)
    {
        return await Db.SysJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobName == jobName && x.JobGroup == jobGroup);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAsync(List<long> ids)
    {
        var rows = await Db.SysJobs.Where(x => ids.Contains(x.Id)).ToListAsync();
        if (rows.Count == 0) return 0;

        Db.SysJobs.RemoveRange(rows);
        return await Db.SaveChangesAsync();
    }
}
