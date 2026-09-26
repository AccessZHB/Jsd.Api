using Jsd.Api.Models.Dict;
using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Services;

/// <summary>
/// 默认缓存实现：进程内 MemoryCache（项目无 Redis 时的替代方案）。
/// 注册为单例 —— MemoryCache 本身线程安全，且不持有任何请求态数据。
/// </summary>
public class MemoryDictCacheStore : IDictCacheStore
{
    private const string KeyPrefix = "dict:";

    private readonly IMemoryCache _cache;

    public MemoryDictCacheStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task<List<DictDataItem>?> GetAsync(string cacheKey)
    {
        return Task.FromResult(_cache.TryGetValue(cacheKey, out List<DictDataItem>? items) ? items : null);
    }

    /// <inheritdoc/>
    public Task SetAsync(string cacheKey, List<DictDataItem> items, int expireMinutes)
    {
        var option = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expireMinutes)
        };

        _cache.Set(cacheKey, items, option);
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
        // MemoryCache 不支持前缀删除，这里把当前缓存中所有 dict: 开头的 Key 摘掉。
        // 换成 Redis 实现时直接用 SCAN + DEL 即可，接口不变。
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
}
