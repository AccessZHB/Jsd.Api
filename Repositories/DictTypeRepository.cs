using Jsd.Api.Entities;
using Jsd.Api.Models.Dict;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>字典类型仓储实现</summary>
public class DictTypeRepository : Repository<SysDictType>, IDictTypeRepository
{
    public DictTypeRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<(List<SysDictType> Items, int Total)> GetPagedListAsync(DictTypeQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 200);

        var q = Db.SysDictTypes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.DictName))
        {
            var name = query.DictName.Trim();
            q = q.Where(x => EF.Functions.Like(x.DictName, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.DictType))
        {
            var code = query.DictType.Trim();
            q = q.Where(x => EF.Functions.Like(x.DictType, $"%{code}%"));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(x => x.Status == query.Status.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<long, int>> GetDataCountMapAsync(List<long> dictTypeIds)
    {
        var map = new Dictionary<long, int>();
        if (dictTypeIds == null || dictTypeIds.Count == 0) return map;

        var rows = await Db.SysDictDatas
            .AsNoTracking()
            .Where(d => dictTypeIds.Contains(d.DictTypeId))
            .GroupBy(d => d.DictTypeId)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var row in rows)
        {
            map[row.Key] = row.Count;
        }

        return map;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string dictType, long excludeId)
    {
        return await Db.SysDictTypes
            .AsNoTracking()
            .AnyAsync(x => x.DictType == dictType && x.Id != excludeId);
    }
}
