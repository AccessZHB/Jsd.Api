using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Services;

/// <summary>
/// 登录会话登记（用于"允许并发登录 / 最大会话数"策略）。
/// 【无 Redis 说明】本项目单机部署，使用进程内 MemoryCache；多实例部署时替换为 Redis 实现即可。
/// access token 中带 sid 声明，TokenVersionMiddleware 逐请求校验会话是否仍有效。
/// </summary>
public interface ISessionRegistry
{
    /// <summary>登记新会话：allowConcurrent=false 时踢掉该用户全部旧会话；否则按 maxSessions 淘汰最旧会话</summary>
    void Register(long userId, string sessionId, bool allowConcurrent, int maxSessions, TimeSpan ttl);

    /// <summary>会话是否仍有效（服务重启后缓存丢失 → 需重新登录，符合单会话策略）</summary>
    bool IsActive(long userId, string sessionId);

    /// <summary>移除指定会话（登出时调用）</summary>
    void Remove(long userId, string sessionId);
}

/// <summary>进程内会话登记实现</summary>
public class SessionRegistry : ISessionRegistry
{
    private readonly IMemoryCache _cache;
    private readonly object _sync = new();

    public SessionRegistry(IMemoryCache cache)
    {
        _cache = cache;
    }

    private static string Key(long userId) => $"login_sessions:{userId}";

    public void Register(long userId, string sessionId, bool allowConcurrent, int maxSessions, TimeSpan ttl)
    {
        var key = Key(userId);
        lock (_sync)
        {
            var sessions = _cache.TryGetValue(key, out List<string>? list) && list != null
                ? list
                : new List<string>();

            if (!allowConcurrent)
            {
                // 单会话：新登录直接踢掉旧会话
                sessions = new List<string> { sessionId };
            }
            else
            {
                sessions.Remove(sessionId);
                sessions.Add(sessionId);

                // 超过最大并发数时淘汰最早的会话
                var max = maxSessions > 0 ? maxSessions : 1;
                if (sessions.Count > max)
                {
                    sessions = sessions.Skip(sessions.Count - max).ToList();
                }
            }

            _cache.Set(key, sessions, ttl);
        }
    }

    public bool IsActive(long userId, string sessionId)
    {
        var key = Key(userId);
        if (!_cache.TryGetValue(key, out List<string>? sessions) || sessions == null)
            return false;
        return sessions.Contains(sessionId);
    }

    public void Remove(long userId, string sessionId)
    {
        var key = Key(userId);
        lock (_sync)
        {
            if (!_cache.TryGetValue(key, out List<string>? sessions) || sessions == null)
                return;

            sessions.Remove(sessionId);
            if (sessions.Count == 0) _cache.Remove(key);
        }
    }
}
