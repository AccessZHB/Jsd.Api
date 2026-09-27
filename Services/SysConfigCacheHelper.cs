using Jsd.Api.Models.System;
using Microsoft.Extensions.Configuration;

namespace Jsd.Api.Services;

/// <summary>
/// 系统配置缓存辅助类：封装 Key 生成、读取、写入、失效、批量预热。
///
/// 缓存策略（与需求文档一致）：
///   Key 格式      ：config:{config_key}，例如 config:security.login
///   正常值 TTL    ：永不过期（写操作显式失效，配置改完立刻生效）
///   空占位 TTL    ：Config:EmptyCacheMinutes（默认 1 分钟）—— 防缓存穿透，
///                   查询不存在的 key 时写入"不存在"占位，避免恶意请求反复打库
///
/// 【为什么不写成静态 ConfigHelper.Get(key)】
///   静态方法拿不到 DI 容器里的缓存实例，只能靠 ServiceLocator 兜底，
///   会破坏可测试性并在启动早期取到 null。这里做成单例服务，注入即用，
///   语义等价于文档里的 ConfigHelper.Get(key)，且能平滑换成 Redis。
/// </summary>
public class SysConfigCacheHelper
{
    /// <summary>缓存 Key 前缀（Redis 实现沿用同一前缀即可）</summary>
    public const string KeyPrefix = "config:";

    /// <summary>空占位默认过期时间（分钟）</summary>
    public const int DefaultEmptyExpireMinutes = 1;

    private readonly ISysConfigCacheStore _store;
    private readonly int _emptyExpireMinutes;

    public SysConfigCacheHelper(ISysConfigCacheStore store, IConfiguration configuration)
    {
        _store = store;

        var emptyMinutes = DefaultEmptyExpireMinutes;
        if (int.TryParse(configuration["Config:EmptyCacheMinutes"], out var e) && e > 0) emptyMinutes = e;

        _emptyExpireMinutes = emptyMinutes;
    }

    /// <summary>生成缓存 Key</summary>
    public static string BuildKey(string configKey) => $"{KeyPrefix}{configKey}";

    /// <summary>读取缓存，未命中返回 null</summary>
    public Task<ConfigCacheItem?> GetAsync(string configKey)
        => string.IsNullOrWhiteSpace(configKey) ? Task.FromResult<ConfigCacheItem?>(null) : _store.GetAsync(BuildKey(configKey));

    /// <summary>
    /// 写入缓存。库中不存在该 key 时写入短 TTL 占位（Exists=false），存在则永不过期。
    /// </summary>
    public Task SetAsync(string configKey, ConfigCacheItem item)
        => _store.SetAsync(BuildKey(configKey), item, item.Exists ? 0 : _emptyExpireMinutes);

    /// <summary>批量写入（启动预热）</summary>
    public Task SetManyAsync(Dictionary<string, ConfigCacheItem> items)
        => _store.SetManyAsync(items, 0);

    /// <summary>失效单个配置的缓存</summary>
    public Task RemoveAsync(string configKey)
        => string.IsNullOrWhiteSpace(configKey) ? Task.CompletedTask : _store.RemoveAsync(BuildKey(configKey));

    /// <summary>失效全部配置缓存</summary>
    public Task RemoveAllAsync() => _store.RemoveAllAsync();
}
