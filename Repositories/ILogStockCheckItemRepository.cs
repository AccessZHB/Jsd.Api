using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 盘点单明细表仓储接口
/// </summary>
public interface ILogStockCheckItemRepository : IRepository<LogStockCheckItem>
{
    /// <summary>
    /// 根据盘点单ID查询全部明细（按 Id 升序）
    /// </summary>
    Task<List<LogStockCheckItem>> GetByCheckIdAsync(long checkId);

    /// <summary>
    /// 批量新增明细
    /// </summary>
    Task AddRangeAsync(IEnumerable<LogStockCheckItem> items);

    /// <summary>
    /// 删除指定盘点单的全部明细（编辑/删除时清理用）
    /// </summary>
    Task RemoveByCheckIdAsync(long checkId);
}
