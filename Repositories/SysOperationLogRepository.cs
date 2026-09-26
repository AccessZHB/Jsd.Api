using Jsd.Api.Entities;
using Jsd.Api.Models.System;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 操作日志仓储接口
/// </summary>
public interface ISysOperationLogRepository
{
    /// <summary>分页查询（操作人/模块/动作/状态/时间范围筛选，create_time 倒序）</summary>
    Task<(List<SysOperationLog> Items, int Total)> GetPagedListAsync(OperationLogQueryDto query);

    /// <summary>按ID查询详情</summary>
    Task<SysOperationLog?> GetByIdAsync(long id);

    /// <summary>导出用：按筛选条件取全量（不超过 maxCount 条）</summary>
    Task<List<SysOperationLog>> GetListForExportAsync(OperationLogQueryDto query, int maxCount = 5000);

    /// <summary>删除单条（返回是否确实删除了记录）</summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>批量删除（返回删除条数）</summary>
    Task<int> DeleteBatchAsync(List<long> ids);

    /// <summary>清理 cutoff 之前的日志（返回删除条数）</summary>
    Task<int> CleanBeforeAsync(DateTime cutoff);

    /// <summary>写入一条操作日志（供后台异步写入服务调用）</summary>
    Task AddAsync(SysOperationLog entity);

    /// <summary>
    /// 日志统计概览（今日总数 / 成功 / 失败 / 平均耗时）
    /// 聚合在数据库完成，避免把当日全量日志拉到内存。
    /// </summary>
    Task<OperationLogStatisticsDto> GetStatisticsAsync(DateTime todayStart);
}

/// <summary>
/// 操作日志仓储实现
/// </summary>
public class SysOperationLogRepository : ISysOperationLogRepository
{
    private readonly AppDbContext _db;

    public SysOperationLogRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<(List<SysOperationLog> Items, int Total)> GetPagedListAsync(OperationLogQueryDto query)
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
    public Task<SysOperationLog?> GetByIdAsync(long id)
        => _db.SysOperationLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    /// <inheritdoc/>
    public async Task<List<SysOperationLog>> GetListForExportAsync(OperationLogQueryDto query, int maxCount = 5000)
    {
        return await BuildFilterQuery(query)
            .OrderByDescending(x => x.CreateTime)
            .Take(maxCount)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.SysOperationLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return false;
        _db.SysOperationLogs.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBatchAsync(List<long> ids)
    {
        // ExecuteDelete 直接一条 SQL 批删，不先把实体加载进内存
        return await _db.SysOperationLogs.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task<int> CleanBeforeAsync(DateTime cutoff)
    {
        return await _db.SysOperationLogs.Where(x => x.CreateTime < cutoff).ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task AddAsync(SysOperationLog entity)
    {
        await _db.SysOperationLogs.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<OperationLogStatisticsDto> GetStatisticsAsync(DateTime todayStart)
    {
        var q = _db.SysOperationLogs.AsNoTracking().Where(x => x.CreateTime >= todayStart);

        var total = await q.CountAsync();
        var success = await q.CountAsync(x => x.Status == 1);
        var avg = total > 0 ? Math.Round(await q.AverageAsync(x => x.ExecutionTime), 1) : 0;

        return new OperationLogStatisticsDto
        {
            TodayTotal = total,
            TodaySuccess = success,
            TodayFail = total - success,
            AvgExecutionTime = avg
        };
    }

    /// <summary>组装筛选条件（分页 / 导出共用）</summary>
    private IQueryable<SysOperationLog> BuildFilterQuery(OperationLogQueryDto query)
    {
        var q = _db.SysOperationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.OperatorName))
            q = q.Where(x => x.OperatorName.Contains(query.OperatorName));
        if (!string.IsNullOrWhiteSpace(query.Module))
            q = q.Where(x => x.Module.Contains(query.Module));
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(x => x.Action.Contains(query.Action));
        if (query.Status.HasValue)
            q = q.Where(x => x.Status == query.Status.Value);
        if (query.StartTime.HasValue)
            q = q.Where(x => x.CreateTime >= query.StartTime.Value);
        if (query.EndTime.HasValue)
            q = q.Where(x => x.CreateTime <= query.EndTime.Value);

        return q;
    }
}
