using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 入库单明细表仓储实现
/// </summary>
public class LogStockInItemRepository : Repository<LogStockInItem>, ILogStockInItemRepository
{
    public LogStockInItemRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 根据入库单ID查询全部明细
    /// </summary>
    public async Task<List<LogStockInItem>> GetByStockInIdAsync(long stockInId)
    {
        return await Db.LogStockInItems
            .Where(i => i.StockInId == stockInId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    /// <summary>
    /// 批量新增明细
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<LogStockInItem> items)
    {
        await Db.LogStockInItems.AddRangeAsync(items);
    }

    /// <summary>
    /// 删除指定入库单的全部明细（ExecuteDelete 一次 SQL 完成，事务内执行）
    /// </summary>
    public async Task RemoveByStockInIdAsync(long stockInId)
    {
        await Db.LogStockInItems
            .Where(i => i.StockInId == stockInId)
            .ExecuteDeleteAsync();
    }
}
