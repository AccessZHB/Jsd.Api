using System.IdentityModel.Tokens.Jwt;

namespace Jsd.Api.Helpers;

/// <summary>
/// 从当前请求中提取 Bearer Token 及其过期时间（F-22 登出拉黑使用）。
/// </summary>
public static class BearerTokenHelper
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// 读取 Authorization: Bearer {token} 中的 token 原文。
    /// 不存在或格式不符返回 null（不抛异常）。
    /// </summary>
    public static string? ReadToken(HttpContext? httpContext)
    {
        var header = httpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return null;

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)) return null;

        var token = header[BearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    /// <summary>
    /// 读取 Token 的过期时间（UTC）。解析失败返回 null（调用方应视为"无需拉黑"）。
    /// </summary>
    public static DateTimeOffset? ReadExpiry(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo == DateTime.MinValue) return null;

            // ValidTo 是 UTC；显式按 UTC 构造 Offset，避免服务器本地时区导致 TTL 偏移
            var utc = DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc);
            return new DateTimeOffset(utc);
        }
        catch
        {
            return null;
        }
    }
}
