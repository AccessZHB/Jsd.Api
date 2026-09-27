using Jsd.Api.Entities;
using Jsd.Api.Models.System;

namespace Jsd.Api.Repositories;

/// <summary>
/// 系统配置仓储接口
/// （分页统一在数据库层用 Skip/Take 完成，禁止在内存中分页）
/// </summary>
public interface ISysConfigRepository : IRepository<SysConfig>
{
    /// <summary>分页查询（按名称/键名/类型/状态筛选，排序号升序）</summary>
    Task<(List<SysConfig> Items, int Total)> GetPagedListAsync(SysConfigQueryDto query);

    /// <summary>按配置键取单条（缓存回源用）</summary>
    Task<SysConfig?> GetByKeyAsync(string configKey);

    /// <summary>批量按配置键取（批量取值接口用，一次查询不循环打库）</summary>
    Task<List<SysConfig>> GetByKeysAsync(List<string> configKeys);

    /// <summary>取全部配置（缓存预热用）</summary>
    Task<List<SysConfig>> GetAllAsync();

    /// <summary>配置键是否已存在（excludeId 用于排除自身，修改时不与自己冲突）</summary>
    Task<bool> ExistsAsync(string configKey, long excludeId);
}
