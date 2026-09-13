using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 盘点单主表仓储实现
/// </summary>
public class LogStockCheckRepository : Repository<LogStockCheck>, ILogStockCheckRepository
{
    public LogStockCheckRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询盘点单（明细不在列表中带出，避免数据量过大）
    /// </summary>
    public async Task<(List<LogStockCheck> Items, int Total)> GetPagedListAsync(
        string? keyword, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        var query = Db.LogStockChecks
            .AsNoTracking()
            .AsQueryable();

        // 盘点单号模糊搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(o => o.CheckNo.Contains(keyword));
        }

        // 状态筛选
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        // 创建时间范围（按天查询时 endTime 传当天 23:59:59）
        if (startTime.HasValue)
        {
            query = query.Where(o => o.CreateTime >= startTime.Value);
        }
        if (endTime.HasValue)
        {
            var end = endTime.Value.TimeOfDay == TimeSpan.Zero
                ? endTime.Value.AddDays(1).AddSeconds(-1)
                : endTime.Value;
            query = query.Where(o => o.CreateTime <= end);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    /// 根据ID查询盘点单详情（明细按 Id 升序）
    /// </summary>
    public async Task<LogStockCheck?> GetDetailAsync(long id)
    {
        return await Db.LogStockChecks
            .Include(o => o.Items.OrderBy(i => i.Id))
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    /// <summary>
    /// 盘点单号是否已存在（uk_check_no 唯一索引的业务层兜底校验）
    /// </summary>
    public async Task<bool> CheckNoExistsAsync(string checkNo)
    {
        return await Db.LogStockChecks.AnyAsync(o => o.CheckNo == checkNo);
    }
}
