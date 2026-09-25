using Jsd.Api.Entities;
using Jsd.Api.Models.Price;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 客户等级 仓储实现（mem_member_level）
/// </summary>
public class MemberLevelRepository : Repository<MemMemberLevel>, IMemberLevelRepository
{
    public MemberLevelRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<MemMemberLevel> Items, int Total)> GetPagedListAsync(MemberLevelQueryDto q)
    {
        var query = Db.MemMemberLevels.AsNoTracking().AsQueryable();

        if (q.Status.HasValue)
        {
            query = query.Where(l => l.Status == q.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(l => l.LevelName.Contains(kw) || l.LevelCode.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.SortOrder)
            .ThenByDescending(l => l.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsLevelCodeAsync(string levelCode, long excludeId = 0)
    {
        return await Db.MemMemberLevels.AsNoTracking()
            .AnyAsync(l => l.LevelCode == levelCode && (excludeId == 0 || l.Id != excludeId));
    }

    public async Task<bool> HasEnabledRulesAsync(long levelId)
    {
        // 关联 price_strategy：只有"启用中且在有效期内"的策略才视为真正生效
        var now = DateTime.Now;
        return await Db.PriceRules.AsNoTracking()
            .AnyAsync(r => r.CustomerLevelId == levelId
                           && r.Status == 1
                           && Db.PriceStrategies.Any(s => s.Id == r.StrategyId
                                                          && s.Status == (int)StrategyStatus.Enabled
                                                          && s.EffectiveDate <= now
                                                          && s.ExpireDate >= now));
    }

    public async Task<MemMemberLevel?> GetByIdTrackedAsync(long id)
    {
        return await Db.MemMemberLevels.FirstOrDefaultAsync(l => l.Id == id);
    }
}
