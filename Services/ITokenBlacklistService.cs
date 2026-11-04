namespace Jsd.Api.Services;

/// <summary>
/// 【F-22】Token 黑名单服务：让"退出登录"真正在服务端生效。
///
/// 背景：JWT 是无状态的，原实现登出只让前端删掉本地 Token，服务端仍然认这个 Token，
/// 在有效期内（Jwt:ExpireMinutes=720）任何人拿到它都能继续调用接口。
/// 本服务把已登出的 Token 记入黑名单，直到其自然过期为止。
///
/// 存储：**仅使用 .NET 自带的 IMemoryCache**（项目未引入 Redis）。
///   - Key 形如 `token:blacklist:{SHA256(token)}`；
///   - TTL = 该 Token 的剩余有效期，到期自动清理，不会内存泄漏；
///   - 不存明文 Token，统一存 SHA256 摘要，避免内存 dump 泄露可用凭据。
///
/// ⚠️ 部署约束：内存黑名单是**进程级**的。单实例部署完全够用；
///    若将来做多实例/负载均衡，需要换成 Redis 等共享存储（本接口已为此预留）。
/// </summary>
public interface ITokenBlacklistService
{
    /// <summary>当前存储后端标识（本项目固定为 Memory），用于日志与排查</summary>
    string BackendName { get; }

    /// <summary>
    /// 将 Token 加入黑名单，TTL = 该 Token 的剩余有效期（已过期则直接跳过，避免无意义写入）。
    /// </summary>
    /// <param name="token">原始 JWT 字符串</param>
    /// <param name="expiresAt">Token 的过期时间（UTC）</param>
    Task RevokeAsync(string token, DateTimeOffset expiresAt);

    /// <summary>判断 Token 是否已被拉黑（中间件每个请求调用一次）</summary>
    /// <param name="token">原始 JWT 字符串</param>
    Task<bool> IsRevokedAsync(string token);
}
