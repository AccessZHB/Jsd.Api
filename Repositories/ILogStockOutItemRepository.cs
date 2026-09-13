using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 出库单明细表仓储接口
/// </summary>
public interface ILogStockOutItemRepository : IRepository<LogStockOutItem>
{
    /// <summary>
    /// 根据出库单ID查询全部明细（按 Id 升序）
    /// </summary>
    Task<List<LogStockOutItem>> GetByStockOutIdAsync(long stockOutId);

    /// <summary>
    /// 批量新增明细
    /// </summary>
    Task AddRangeAsync(IEnumerable<LogStockOutItem> items);

    /// <summary>
    /// 删除指定出库单的全部明细（编辑时"删旧明细"用）
    /// </summary>
    Task RemoveByStockOutIdAsync(long stockOutId);
}
