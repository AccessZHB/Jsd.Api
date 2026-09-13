using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 订单明细仓储实现
/// </summary>
public class OrderItemRepository : Repository<TrxOrderItem>, IOrderItemRepository
{
    public OrderItemRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>按订单ID查询明细（按 Id 升序，保证详情展示顺序稳定）</summary>
    public async Task<List<TrxOrderItem>> GetByOrderIdAsync(long orderId)
    {
        return await Db.TrxOrderItems
            .AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    /// <summary>批量新增明细（创建订单时在事务内一次性写入）</summary>
    public async Task AddRangeAsync(List<TrxOrderItem> items)
    {
        await Db.TrxOrderItems.AddRangeAsync(items);
    }
}
