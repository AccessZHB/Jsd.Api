using Jsd.Api.Entities;
using Jsd.Api.Models.System;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>系统配置仓储实现</summary>
public class SysConfigRepository : Repository<SysConfig>, ISysConfigRepository
{
    public SysConfigRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<(List<SysConfig> Items, int Total)> GetPagedListAsync(SysConfigQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 200);

        var q = Db.SysConfigs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ConfigName))
        {
            var name = query.ConfigName.Trim();
            q = q.Where(x => EF.Functions.Like(x.ConfigName, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.ConfigKey))
        {
            var key = query.ConfigKey.Trim();
            q = q.Where(x => EF.Functions.Like(x.ConfigKey, $"%{key}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.ConfigType))
        {
            var type = query.ConfigType.Trim();
            q = q.Where(x => x.ConfigType == type);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(x => x.Status == query.Status.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public Task<SysConfig?> GetByKeyAsync(string configKey)
        => Db.SysConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.ConfigKey == configKey);

    /// <inheritdoc/>
    public async Task<List<SysConfig>> GetByKeysAsync(List<string> configKeys)
    {
        if (configKeys == null || configKeys.Count == 0) return new List<SysConfig>();

        // 去重，避免重复 key 造成重复查询与重复返回
        var keys = configKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keys.Count == 0) return new List<SysConfig>();

        return await Db.SysConfigs.AsNoTracking().Where(x => keys.Contains(x.ConfigKey)).ToListAsync();
    }

    /// <inheritdoc/>
    public Task<List<SysConfig>> GetAllAsync()
        => Db.SysConfigs.AsNoTracking().ToListAsync();

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string configKey, long excludeId)
        => Db.SysConfigs.AsNoTracking().AnyAsync(x => x.ConfigKey == configKey && x.Id != excludeId);
}
