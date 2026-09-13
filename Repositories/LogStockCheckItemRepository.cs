using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 盘点单明细表仓储实现
/// </summary>
public class LogStockCheckItemRepository : Repository<LogStockCheckItem>, ILogStockCheckItemRepository
{
    public LogStockCheckItemRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 根据盘点单ID查询全部明细
    /// </summary>
    public async Task<List<LogStockCheckItem>> GetByCheckIdAsync(long checkId)
    {
        return await Db.LogStockCheckItems
            .Where(i => i.CheckId == checkId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    /// <summary>
    /// 批量新增明细
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<LogStockCheckItem> items)
    {
        await Db.LogStockCheckItems.AddRangeAsync(items);
    }

    /// <summary>
    /// 删除指定盘点单的全部明细（ExecuteDelete 一次 SQL 完成，事务内执行）
    /// </summary>
    public async Task RemoveByCheckIdAsync(long checkId)
    {
        await Db.LogStockCheckItems
            .Where(i => i.CheckId == checkId)
            .ExecuteDeleteAsync();
    }
}
