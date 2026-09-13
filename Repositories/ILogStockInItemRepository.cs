using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 入库单明细表仓储接口
/// </summary>
public interface ILogStockInItemRepository : IRepository<LogStockInItem>
{
    /// <summary>
    /// 根据入库单ID查询全部明细（按 Id 升序）
    /// </summary>
    Task<List<LogStockInItem>> GetByStockInIdAsync(long stockInId);

    /// <summary>
    /// 批量新增明细
    /// </summary>
    Task AddRangeAsync(IEnumerable<LogStockInItem> items);

    /// <summary>
    /// 删除指定入库单的全部明细（编辑时"删旧明细"用）
    /// </summary>
    Task RemoveByStockInIdAsync(long stockInId);
}
