using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Services;

/// <summary>
/// 【F-22】Token 黑名单实现 —— 纯 IMemoryCache（项目未引入 Redis）。
///
/// 关键设计：
///   1. **Key 不落明文**：`token:blacklist:{SHA256(token)}`，避免内存/日志里出现可直接使用的凭据；
///   2. **TTL = Token 剩余有效期**：到期后缓存自动回收，不会无限增长（Requirement 2.3）；
///   3. **已过期直接跳过**：Token 本身已失效时写黑名单没有意义，省一次写入；
///   4. **判空防御**：空 Token 一律视为"未拉黑"，不抛异常；
///   5. 本服务无 IO，方法返回 Task 只是为了保持接口形态，便于将来替换为 Redis 实现。
/// </summary>
public class TokenBlacklistService : ITokenBlacklistService
{
    /// <summary>缓存键前缀：token:blacklist:{sha256}</summary>
    private const string KeyPrefix = "token:blacklist:";
    private const string FlagValue = "1";

    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenBlacklistService> _logger;

    public TokenBlacklistService(IMemoryCache cache, ILogger<TokenBlacklistService> logger)
    {
        _cache = cache;
        _logger = logger;
        _logger.LogInformation("Token 黑名单使用进程内 IMemoryCache（单实例部署足够；多实例需替换为共享存储）");
    }

    /// <inheritdoc />
    public string BackendName => "Memory";

    /// <inheritdoc />
    public Task RevokeAsync(string token, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.CompletedTask;

        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            // Token 已自然过期，JWT 校验会直接拒绝，无需再写黑名单
            return Task.CompletedTask;
        }

        _cache.Set(KeyPrefix + Sha256(token), FlagValue, ttl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsRevokedAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult(false);

        var revoked = _cache.TryGetValue(KeyPrefix + Sha256(token), out _);
        return Task.FromResult(revoked);
    }

    /// <summary>Token 摘要：sha256 十六进制，避免明文入缓存</summary>
    private static string Sha256(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
