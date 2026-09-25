using System.Security.Cryptography;
using Jsd.Api.Entities;
using Jsd.Api.Models.Price;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 价格策略 仓储实现（price_strategy）
/// </summary>
public class PriceStrategyRepository : Repository<PriceStrategy>, IPriceStrategyRepository
{
    public PriceStrategyRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<PriceStrategy> Items, int Total)> GetPagedListAsync(PriceStrategyQueryDto q)
    {
        var query = Db.PriceStrategies.AsNoTracking().AsQueryable();

        if (q.StrategyType.HasValue)
        {
            query = query.Where(s => s.StrategyType == q.StrategyType.Value);
        }

        if (q.Status.HasValue)
        {
            query = query.Where(s => s.Status == q.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(s => s.StrategyNo.Contains(kw) || s.StrategyName.Contains(kw));
        }

        // 日期范围：按生效时间过滤（与时间窗有交集即命中）
        if (q.StartTime.HasValue)
        {
            query = query.Where(s => s.EffectiveDate >= q.StartTime.Value);
        }
        if (q.EndTime.HasValue)
        {
            query = query.Where(s => s.EffectiveDate < q.EndTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.Priority)
            .ThenByDescending(s => s.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsStrategyNoAsync(string strategyNo)
    {
        return await Db.PriceStrategies.AsNoTracking().AnyAsync(s => s.StrategyNo == strategyNo);
    }

    public string GenerateStrategyNo()
    {
        // 撞号由数据库 uk_strategy_no 唯一索引兜底，Service 层重试
        var rand = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"PS{DateTime.Now:yyyyMMddHHmmss}{rand}";
    }

    public async Task<PriceStrategy?> GetByIdTrackedAsync(long id)
    {
        return await Db.PriceStrategies.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<PriceStrategy>> GetEffectiveStrategiesAsync(DateTime now)
    {
        return await Db.PriceStrategies.AsNoTracking()
            .Where(s => s.Status == (int)StrategyStatus.Enabled
                        && s.EffectiveDate <= now
                        && s.ExpireDate >= now)
            .OrderByDescending(s => s.Priority)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    public async Task<Dictionary<long, int>> CountRulesAsync(List<long> strategyIds)
    {
        if (strategyIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        return await Db.PriceRules.AsNoTracking()
            .Where(r => strategyIds.Contains(r.StrategyId))
            .GroupBy(r => r.StrategyId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }
}
