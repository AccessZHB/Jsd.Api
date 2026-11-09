using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 订单主表仓储实现。
/// 所有查询默认过滤 is_deleted=0（逻辑删除）。
/// </summary>
public class OrderRepository : Repository<TrxOrder>, IOrderRepository
{
    public OrderRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询订单（订单号 / 状态 / 创建时间范围 筛选）
    /// 列表不带出明细（明细数量由 Service 用 group by 一次性统计，避免 N+1）
    /// </summary>
    public async Task<(List<TrxOrder> Items, int Total)> GetPagedListAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime,
        long? buyerId, string? buyerKeyword, string? receiverKeyword,
        int page, int pageSize, long? lastId)
    {
        var query = Db.TrxOrders
            .AsNoTracking()
            .Where(o => o.IsDeleted == 0);   // 逻辑删除过滤

        // 订单号模糊搜索
        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            query = query.Where(o => o.OrderNo.Contains(orderNo));
        }

        // 买家ID精确筛选
        if (buyerId.HasValue)
        {
            query = query.Where(o => o.BuyerId == buyerId.Value);
        }

        // 买家名称模糊筛选（按 mem_member.name，子查询避免额外 join 污染分页）
        if (!string.IsNullOrWhiteSpace(buyerKeyword))
        {
            var kw = buyerKeyword.Trim();
            query = query.Where(o => Db.MemMembers
                .Any(m => m.Id == o.BuyerId && m.IsDeleted == 0 && m.Name.Contains(kw)));
        }

        // 收货人姓名 / 手机号模糊筛选
        if (!string.IsNullOrWhiteSpace(receiverKeyword))
        {
            var kw = receiverKeyword.Trim();
            query = query.Where(o => o.ReceiverName.Contains(kw) || o.ReceiverPhone.Contains(kw));
        }

        // 订单状态筛选
        if (orderStatus.HasValue)
        {
            query = query.Where(o => o.OrderStatus == orderStatus.Value);
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

        // 游标分页（推荐）：lastId > 0 时按主键倒序取下一页，
        // 不再执行 COUNT(*)，也不再使用 Skip/OFFSET，避免百万级深翻页慢查询。
        var useCursor = lastId.HasValue && lastId.Value > 0;

        if (useCursor)
        {
            var cursorItems = await query
                .Where(o => o.Id < lastId!.Value)
                .OrderByDescending(o => o.Id)
                .Take(pageSize)
                .ToListAsync();

            return (cursorItems, 0);   // 游标模式不返回总数（由 hasMore/nextLastId 表达）
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
    /// 根据ID查询订单详情（含明细，明细按 Id 升序）
    /// </summary>
    public async Task<TrxOrder?> GetDetailAsync(long id)
    {
        return await Db.TrxOrders
            .Include(o => o.Items.OrderBy(i => i.Id))
            .FirstOrDefaultAsync(o => o.Id == id && o.IsDeleted == 0);
    }

    /// <summary>
    /// 订单号是否已存在（uk_order_no 唯一索引的业务层兜底校验）
    /// </summary>
    public async Task<bool> OrderNoExistsAsync(string orderNo)
    {
        return await Db.TrxOrders.AnyAsync(o => o.OrderNo == orderNo);
    }
}
