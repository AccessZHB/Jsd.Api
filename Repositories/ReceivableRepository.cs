using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 应收账款 仓储实现
///
/// 【联查策略】
///   trx_receivable 与 trx_order / mem_member 之间没有定义外键导航属性
///   （保持表结构轻量、避免 EF 影子外键），因此这里统一采用：
///     1) 用 EXISTS 子查询做筛选（翻译可靠、不会因 join 产生笛卡尔放大）
///     2) 分页取回实体后，用字典批量补全订单号 / 客户名称（避免 N+1 查询）
/// </summary>
public class ReceivableRepository : Repository<TrxReceivable>, IReceivableRepository
{
    public ReceivableRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<ReceivableRow> Items, int Total)> GetPagedListAsync(
        long? memberId, string? memberName, string? orderNo, int? status,
        DateTime? dueStart, DateTime? dueEnd, int page, int pageSize)
    {
        var query = Db.TrxReceivables.AsNoTracking().AsQueryable();

        // 客户ID（精确匹配）——核销选单场景用，避免同名客户串单
        if (memberId.HasValue)
        {
            query = query.Where(r => r.MemberId == memberId.Value);
        }

        // 客户名称（模糊）：EXISTS 子查询，避免 join 后分页错乱
        if (!string.IsNullOrWhiteSpace(memberName))
        {
            var kw = memberName.Trim();
            query = query.Where(r => Db.MemMembers.Any(m => m.Id == r.MemberId && m.Name.Contains(kw)));
        }

        // 订单号（模糊）
        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            var kw = orderNo.Trim();
            query = query.Where(r => Db.TrxOrders.Any(o => o.Id == r.OrderId && o.OrderNo.Contains(kw)));
        }

        // 结算状态
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        // 到期日范围（前端"到期日期"筛选；dueEnd 含当天，故 +1 天后取开区间）
        if (dueStart.HasValue)
        {
            query = query.Where(r => r.DueDate >= dueStart.Value.Date);
        }
        if (dueEnd.HasValue)
        {
            query = query.Where(r => r.DueDate < dueEnd.Value.Date.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 批量补全订单号与客户名称（两次查询搞定，避免逐行查库）
        var orderIds = items.Select(r => r.OrderId).Distinct().ToList();
        var memberIds = items.Select(r => r.MemberId).Distinct().ToList();

        var orderNoMap = await Db.TrxOrders.AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNo })
            .ToDictionaryAsync(x => x.Id, x => x.OrderNo);

        var memberNameMap = await Db.MemMembers.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var rows = items.Select(r => new ReceivableRow
        {
            Receivable = r,
            OrderNo = orderNoMap.TryGetValue(r.OrderId, out var no) ? no : string.Empty,
            MemberName = memberNameMap.TryGetValue(r.MemberId, out var name) ? name : string.Empty
        }).ToList();

        return (rows, total);
    }

    public async Task<TrxReceivable?> GetByIdTrackedAsync(long id)
    {
        // 带跟踪：核销时直接改实体属性即可被 EF 感知
        return await Db.TrxReceivables.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<TrxReceivable?> GetByOrderIdAsync(long orderId)
    {
        return await Db.TrxReceivables.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderId == orderId);
    }

    public async Task<List<WriteOffRow>> GetWriteOffsAsync(long receivableId)
    {
        // 内连接：mkt_payment_item → mkt_payment（取收款单号/方式/到账日期）
        var query = from pi in Db.MktPaymentItems.AsNoTracking()
                    join p in Db.MktPayments.AsNoTracking() on pi.PaymentId equals p.Id
                    where pi.ReceivableId == receivableId
                    orderby pi.Id descending
                    select new WriteOffRow
                    {
                        Item = pi,
                        PaymentNo = p.PaymentNo,
                        PaymentMethod = p.PaymentMethod,
                        PaymentDate = p.PaymentDate
                    };

        return await query.ToListAsync();
    }
}
