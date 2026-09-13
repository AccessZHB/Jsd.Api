using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 订单明细仓储接口（继承泛型仓储，额外提供按订单查询与批量新增）
/// </summary>
public interface IOrderItemRepository : IRepository<TrxOrderItem>
{
    /// <summary>按订单ID查询明细（按 Id 升序）</summary>
    Task<List<TrxOrderItem>> GetByOrderIdAsync(long orderId);

    /// <summary>批量新增明细（创建订单时一次性写入）</summary>
    Task AddRangeAsync(List<TrxOrderItem> items);
}
