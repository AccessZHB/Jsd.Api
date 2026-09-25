using Jsd.Api.Entities;
using Jsd.Api.Models.Price;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 余额流水 仓储实现（mkt_balance_log）
/// 列表查询沿用项目统一策略：EXISTS 子查询筛选 + 分页取回 + Service 层字典批量补全，
/// 避免 join 导致分页错乱与 N+1。
/// </summary>
public class BalanceLogRepository : Repository<MktBalanceLog>, IBalanceLogRepository
{
    public BalanceLogRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<MktBalanceLog> Items, int Total)> GetPagedListAsync(BalanceLogQueryDto q)
    {
        var query = Db.MktBalanceLogs.AsNoTracking().AsQueryable();

        if (q.MemMemberId.HasValue)
        {
            query = query.Where(l => l.MemMemberId == q.MemMemberId.Value);
        }

        // 会员名称/编号模糊：EXISTS 子查询关联 mem_member
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(l =>
                Db.MemMembers.Any(m => m.Id == l.MemMemberId && (m.Name.Contains(kw) || m.MemberNo.Contains(kw))));
        }

        if (q.ChangeType.HasValue)
        {
            query = query.Where(l => l.ChangeType == q.ChangeType.Value);
        }

        if (q.RelatedType.HasValue)
        {
            query = query.Where(l => l.RelatedType == q.RelatedType.Value);
        }

        if (q.StartTime.HasValue)
        {
            query = query.Where(l => l.CreateTime >= q.StartTime.Value);
        }
        if (q.EndTime.HasValue)
        {
            query = query.Where(l => l.CreateTime < q.EndTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<MktBalanceLog?> GetByRelatedAsync(long memMemberId, int relatedType, long relatedId, int changeType)
    {
        return await Db.MktBalanceLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.MemMemberId == memMemberId
                                      && l.RelatedType == relatedType
                                      && l.RelatedId == relatedId
                                      && l.ChangeType == changeType);
    }
}
