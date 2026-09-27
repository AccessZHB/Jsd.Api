using Jsd.Api.Models.System;

namespace Jsd.Api.Services;

/// <summary>
/// 系统配置缓存存储抽象。
///
/// 与字典缓存（IDictCacheStore）同一套路：默认走进程内 MemoryCache，
/// 多实例部署时只要新增一个 Redis 实现并在 Program.cs 换注册，上层
/// SysConfigCacheHelper / SysConfigService 一行都不用改。
/// </summary>
public interface ISysConfigCacheStore
{
    /// <summary>读取缓存项，未命中返回 null</summary>
    Task<ConfigCacheItem?> GetAsync(string cacheKey);

    /// <summary>
    /// 写入缓存。expireMinutes &lt;= 0 表示永不过期（正常配置值）；
    /// &gt; 0 表示 N 分钟后过期（不存在的 key 的空占位，防缓存穿透）。
    /// </summary>
    Task SetAsync(string cacheKey, ConfigCacheItem item, int expireMinutes);

    /// <summary>删除单个缓存</summary>
    Task RemoveAsync(string cacheKey);

    /// <summary>删除全部配置缓存（config: 前缀）</summary>
    Task RemoveAllAsync();

    /// <summary>批量写入（启动预热一次性装载）</summary>
    Task SetManyAsync(Dictionary<string, ConfigCacheItem> items, int expireMinutes);
}
