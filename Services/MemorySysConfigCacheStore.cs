using Jsd.Api.Models.System;
using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Services;

/// <summary>
/// 系统配置缓存默认实现：进程内 MemoryCache（项目无 Redis 时的替代方案）。
/// 注册为单例 —— MemoryCache 本身线程安全，且不持有任何请求态数据。
/// </summary>
public class MemorySysConfigCacheStore : ISysConfigCacheStore
{
    /// <summary>缓存 Key 前缀（Redis 实现沿用同一前缀即可）</summary>
    public const string KeyPrefix = "config:";

    private readonly IMemoryCache _cache;

    public MemorySysConfigCacheStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task<ConfigCacheItem?> GetAsync(string cacheKey)
        => Task.FromResult(_cache.TryGetValue(cacheKey, out ConfigCacheItem? item) ? item : null);

    /// <inheritdoc/>
    public Task SetAsync(string cacheKey, ConfigCacheItem item, int expireMinutes)
    {
        if (expireMinutes > 0)
        {
            _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expireMinutes)
            });
        }
        else
        {
            // 永不过期：正常配置值靠写操作显式失效，不用 TTL 兜底
            _cache.Set(cacheKey, item);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string cacheKey)
    {
        _cache.Remove(cacheKey);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAllAsync()
    {
        // MemoryCache 不支持前缀删除，这里摘掉所有 config: 开头的 Key。
        // 换成 Redis 实现时直接 SCAN + DEL 即可，接口不变。
        if (_cache is not MemoryCache memoryCache) return Task.CompletedTask;

        var keys = memoryCache.Keys
            .Where(k => k is string s && s.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            .Cast<object>()
            .ToList();

        foreach (var key in keys)
        {
            _cache.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetManyAsync(Dictionary<string, ConfigCacheItem> items, int expireMinutes)
    {
        foreach (var item in items)
        {
            if (expireMinutes > 0)
            {
                _cache.Set(item.Key, item.Value, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expireMinutes)
                });
            }
            else
            {
                _cache.Set(item.Key, item.Value);
            }
        }

        return Task.CompletedTask;
    }
}
