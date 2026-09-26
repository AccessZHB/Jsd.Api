using Jsd.Api.Models.Dict;

namespace Jsd.Api.Services;

/// <summary>
/// 字典缓存辅助类：封装 Key 生成、读取、写入、删除、预热。
///
/// 缓存策略（与需求文档一致）：
///   Key 格式    ：dict:{dictType}，例如 dict:sys_order_status
///   正常数据 TTL ：Dict:CacheMinutes（appsettings 可配，默认 30 分钟）
///   空结果 TTL   ：Dict:EmptyCacheMinutes（默认 5 分钟）—— 防缓存穿透，
///                  查询一个不存在的 dictType 时也会缓存空列表，避免每次都打数据库
/// </summary>
public class DictCacheHelper
{
    /// <summary>缓存 Key 前缀（Redis 实现沿用同一前缀即可）</summary>
    public const string KeyPrefix = "dict:";

    /// <summary>默认过期时间（分钟）</summary>
    public const int DefaultExpireMinutes = 30;

    /// <summary>空结果过期时间（分钟），用于缓存穿透防护</summary>
    public const int DefaultEmptyExpireMinutes = 5;

    private readonly IDictCacheStore _store;
    private readonly int _defaultExpireMinutes;
    private readonly int _emptyExpireMinutes;

    public DictCacheHelper(IDictCacheStore store, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _store = store;

        var cacheMinutes = DefaultExpireMinutes;
        if (int.TryParse(configuration["Dict:CacheMinutes"], out var m) && m > 0) cacheMinutes = m;

        var emptyMinutes = DefaultEmptyExpireMinutes;
        if (int.TryParse(configuration["Dict:EmptyCacheMinutes"], out var e) && e > 0) emptyMinutes = e;

        _defaultExpireMinutes = cacheMinutes;
        _emptyExpireMinutes = emptyMinutes;
    }

    /// <summary>生成缓存 Key</summary>
    public static string BuildKey(string dictType) => $"{KeyPrefix}{dictType}";

    /// <summary>读取缓存，未命中返回 null</summary>
    public Task<List<DictDataItem>?> GetAsync(string dictType)
        => _store.GetAsync(BuildKey(dictType));

    /// <summary>
    /// 写入缓存。空列表同样写入（短 TTL），这样前端反复请求一个不存在的字典类型
    /// 也不会每次都穿透到数据库。
    /// </summary>
    public Task SetAsync(string dictType, List<DictDataItem> items)
        => _store.SetAsync(BuildKey(dictType), items,
                           items.Count == 0 ? _emptyExpireMinutes : _defaultExpireMinutes);

    /// <summary>失效单个字典类型的缓存</summary>
    public Task RemoveAsync(string dictType)
        => string.IsNullOrWhiteSpace(dictType) ? Task.CompletedTask : _store.RemoveAsync(BuildKey(dictType));

    /// <summary>失效全部字典缓存</summary>
    public Task RemoveAllAsync() => _store.RemoveAllAsync();
}
