using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 出库单明细表仓储实现
/// </summary>
public class LogStockOutItemRepository : Repository<LogStockOutItem>, ILogStockOutItemRepository
{
    public LogStockOutItemRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 根据出库单ID查询全部明细
    /// </summary>
    public async Task<List<LogStockOutItem>> GetByStockOutIdAsync(long stockOutId)
    {
        return await Db.LogStockOutItems
            .Where(i => i.StockOutId == stockOutId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    /// <summary>
    /// 批量新增明细
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<LogStockOutItem> items)
    {
        await Db.LogStockOutItems.AddRangeAsync(items);
    }

    /// <summary>
    /// 删除指定出库单的全部明细（ExecuteDelete 一次 SQL 完成，事务内执行）
    /// </summary>
    public async Task RemoveByStockOutIdAsync(long stockOutId)
    {
        await Db.LogStockOutItems
            .Where(i => i.StockOutId == stockOutId)
            .ExecuteDeleteAsync();
    }
}
