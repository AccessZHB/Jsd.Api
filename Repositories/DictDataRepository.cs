using Jsd.Api.Entities;
using Jsd.Api.Models.Dict;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>字典数据仓储实现</summary>
public class DictDataRepository : Repository<SysDictData>, IDictDataRepository
{
    public DictDataRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<(List<SysDictData> Items, int Total)> GetPagedListAsync(DictDataQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 200);

        var q = Db.SysDictDatas.AsNoTracking().AsQueryable();

        if (query.DictTypeId.HasValue)
        {
            q = q.Where(d => d.DictTypeId == query.DictTypeId.Value);
        }

        // 按「字典类型编码」筛选：sys_dict_data 不冗余存编码，联表取该编码对应的类型ID再过滤。
        // 先查出 ID 集合再 Contains，避免手写 JOIN 让分页计数变复杂。
        if (!string.IsNullOrWhiteSpace(query.DictTypeCode))
        {
            var code = query.DictTypeCode.Trim();
            var typeIds = await Db.SysDictTypes
                .AsNoTracking()
                .Where(t => t.DictType == code)
                .Select(t => t.Id)
                .ToListAsync();

            if (typeIds.Count == 0) return (new List<SysDictData>(), 0);

            q = q.Where(d => typeIds.Contains(d.DictTypeId));
        }

        if (!string.IsNullOrWhiteSpace(query.DictLabel))
        {
            var label = query.DictLabel.Trim();
            q = q.Where(d => EF.Functions.Like(d.DictLabel, $"%{label}%"));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(d => d.Status == query.Status.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(d => d.DictSort)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<List<SysDictData>> GetEnabledListByDictTypeAsync(string dictType)
    {
        var type = await Db.SysDictTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.DictType == dictType);

        if (type == null) return new List<SysDictData>();

        return await Db.SysDictDatas
            .AsNoTracking()
            .Where(d => d.DictTypeId == type.Id && d.Status == 0)
            .OrderBy(d => d.DictSort)
            .ThenBy(d => d.Id)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetEnabledTypeCodesAsync()
    {
        return await Db.SysDictTypes
            .AsNoTracking()
            .Where(t => t.Status == 0)
            .OrderBy(t => t.Id)
            .Select(t => t.DictType)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByDictTypeIdAsync(long dictTypeId)
    {
        // ExecuteDelete：一条 DELETE 语句直接在数据库批量删，不把数据加载到内存
        return await Db.SysDictDatas
            .Where(d => d.DictTypeId == dictTypeId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByIdsAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return 0;

        return await Db.SysDictDatas
            .Where(d => ids.Contains(d.Id))
            .ExecuteDeleteAsync();
    }
}
