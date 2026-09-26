using Jsd.Api.Models.Dict;

namespace Jsd.Api.Services;

/// <summary>
/// 字典缓存仓储抽象。
///
/// 【为什么要有这层接口】项目目前没有 Redis，默认实现是 MemoryDictCacheStore（进程内内存缓存）。
/// 多实例部署时只需再写一个 RedisDictCacheStore 并在 Program.cs 换掉这一行注册即可，
/// 上层 DictCacheHelper / DictCacheService / Controller 全部不用改。
/// </summary>
public interface IDictCacheStore
{
    /// <summary>读取缓存，未命中或已过期返回 null</summary>
    Task<List<DictDataItem>?> GetAsync(string cacheKey);

    /// <summary>写入缓存（过期时间由调用方给定分钟数）</summary>
    Task SetAsync(string cacheKey, List<DictDataItem> items, int expireMinutes);

    /// <summary>删除单个 Key</summary>
    Task RemoveAsync(string cacheKey);

    /// <summary>清空全部字典缓存（遍历所有可能的 Key 代价高，这里统一用前缀扫描实现）</summary>
    Task RemoveAllAsync();
}
