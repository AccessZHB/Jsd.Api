using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 出库单主表仓储实现
/// </summary>
public class LogStockOutRepository : Repository<LogStockOut>, ILogStockOutRepository
{
    public LogStockOutRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询出库单（明细不在列表中带出，避免数据量过大）
    /// </summary>
    public async Task<(List<LogStockOut> Items, int Total)> GetPagedListAsync(
        string? keyword, int? outType, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        var query = Db.LogStockOuts
            .AsNoTracking()
            .AsQueryable();

        // 出库单号模糊搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(o => o.StockOutNo.Contains(keyword));
        }

        // 出库类型筛选
        if (outType.HasValue)
        {
            query = query.Where(o => o.OutType == outType.Value);
        }

        // 状态筛选
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        // 创建时间范围（按天查询时 endTime 传当天 23:59:59，或直接传日期此处自动补到当天结束）
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
    /// 根据ID查询出库单详情（明细按 Id 升序）
    /// </summary>
    public async Task<LogStockOut?> GetDetailAsync(long id)
    {
        return await Db.LogStockOuts
            .Include(o => o.Items.OrderBy(i => i.Id))
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    /// <summary>
    /// 出库单号是否已存在（uk_stock_out_no 唯一索引的业务层兜底校验）
    /// </summary>
    public async Task<bool> StockOutNoExistsAsync(string stockOutNo)
    {
        return await Db.LogStockOuts.AnyAsync(o => o.StockOutNo == stockOutNo);
    }
}
