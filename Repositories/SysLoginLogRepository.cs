using Jsd.Api.Entities;
using Jsd.Api.Models.System;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 登录日志仓储接口
/// </summary>
public interface ISysLoginLogRepository
{
    /// <summary>分页查询（用户名/IP/登录方式/状态/时间范围筛选，create_time 倒序）</summary>
    Task<(List<SysLoginLog> Items, int Total)> GetPagedListAsync(LoginLogQueryDto query);

    /// <summary>按ID查询详情</summary>
    Task<SysLoginLog?> GetByIdAsync(long id);

    /// <summary>删除单条（返回是否确实删除了记录）</summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>批量删除（返回删除条数）</summary>
    Task<int> DeleteBatchAsync(List<long> ids);

    /// <summary>清理 cutoff 之前的日志（返回删除条数）</summary>
    Task<int> CleanBeforeAsync(DateTime cutoff);

    /// <summary>写入一条登录日志（登录成功/失败均写入）</summary>
    Task AddAsync(SysLoginLog entity);

    /// <summary>导出用：按筛选条件取全量（不超过 maxCount 条）</summary>
    Task<List<SysLoginLog>> GetListForExportAsync(LoginLogQueryDto query, int maxCount = 5000);

    /// <summary>
    /// 批量查询指定账号"最近一条失败登录"的登录方式（用于锁定账号页展示 login_type）
    /// 返回 (MemberId, LoginType)；查不到的账号不出现在结果里。
    /// </summary>
    Task<List<(long MemberId, int LoginType)>> GetLastFailLoginTypesAsync(List<long> memberIds);

    /// <summary>
    /// 登录日志统计概览（今日总数 / 成功 / 失败 / 当前锁定账号数）
    /// 聚合在数据库完成；锁定账号数来自 sys_user（lock_until 未过期）。
    /// </summary>
    Task<LoginLogStatisticsDto> GetStatisticsAsync(DateTime todayStart);
}

/// <summary>
/// 登录日志仓储实现
/// </summary>
public class SysLoginLogRepository : ISysLoginLogRepository
{
    private readonly AppDbContext _db;

    public SysLoginLogRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<(List<SysLoginLog> Items, int Total)> GetPagedListAsync(LoginLogQueryDto query)
    {
        var q = BuildFilterQuery(query);
        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(x => x.CreateTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync();
        return (items, total);
    }

    /// <inheritdoc/>
    public Task<SysLoginLog?> GetByIdAsync(long id)
        => _db.SysLoginLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.SysLoginLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return false;
        _db.SysLoginLogs.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBatchAsync(List<long> ids)
    {
        return await _db.SysLoginLogs.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task<int> CleanBeforeAsync(DateTime cutoff)
    {
        return await _db.SysLoginLogs.Where(x => x.CreateTime < cutoff).ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task AddAsync(SysLoginLog entity)
    {
        await _db.SysLoginLogs.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<List<(long MemberId, int LoginType)>> GetLastFailLoginTypesAsync(List<long> memberIds)
    {
        if (memberIds.Count == 0) return new List<(long, int)>();

        // 每个账号取最近一条失败登录：按 create_time 倒序，在客户端按账号分组取第一条
        // （EF Core 9 不支持 GroupBy 后的 Take(1) 直译，这里数据量可控，先取候选再内存分组）
        var logs = await _db.SysLoginLogs
            .AsNoTracking()
            .Where(x => x.MemberId.HasValue && memberIds.Contains(x.MemberId.Value) && x.Status == 0)
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new { x.MemberId, x.LoginType })
            .ToListAsync();

        var result = new List<(long MemberId, int LoginType)>();
        var seen = new HashSet<long>();
        foreach (var log in logs)
        {
            if (log.MemberId.HasValue && seen.Add(log.MemberId.Value))
                result.Add((log.MemberId.Value, log.LoginType));
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<List<SysLoginLog>> GetListForExportAsync(LoginLogQueryDto query, int maxCount = 5000)
    {
        return await BuildFilterQuery(query)
            .OrderByDescending(x => x.CreateTime)
            .Take(maxCount)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<LoginLogStatisticsDto> GetStatisticsAsync(DateTime todayStart)
    {
        var q = _db.SysLoginLogs.AsNoTracking().Where(x => x.CreateTime >= todayStart);

        var total = await q.CountAsync();
        var success = await q.CountAsync(x => x.Status == 1);
        var locked = await _db.SysUsers.AsNoTracking()
            .CountAsync(u => u.LockUntil != null && u.LockUntil > DateTime.Now);

        return new LoginLogStatisticsDto
        {
            TodayTotal = total,
            TodaySuccess = success,
            TodayFail = total - success,
            LockedCount = locked
        };
    }

    /// <summary>组装筛选条件</summary>
    private IQueryable<SysLoginLog> BuildFilterQuery(LoginLogQueryDto query)
    {
        var q = _db.SysLoginLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.MemberName))
            q = q.Where(x => x.MemberName.Contains(query.MemberName));
        if (!string.IsNullOrWhiteSpace(query.IpAddress))
            q = q.Where(x => x.IpAddress.Contains(query.IpAddress));
        if (query.LoginType.HasValue)
            q = q.Where(x => x.LoginType == query.LoginType.Value);
        if (query.Status.HasValue)
            q = q.Where(x => x.Status == query.Status.Value);
        if (query.StartTime.HasValue)
            q = q.Where(x => x.CreateTime >= query.StartTime.Value);
        if (query.EndTime.HasValue)
            q = q.Where(x => x.CreateTime <= query.EndTime.Value);

        return q;
    }
}
