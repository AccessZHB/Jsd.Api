using System.Text.Json;
using Jsd.Api.Entities;
using Jsd.Api.Repositories;
using Jsd.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Middlewares;

/// <summary>
/// Token 密码版本校验中间件（改密强制下线）。
/// JWT 里带 ver 声明（签发时的 pwd_version）；请求到达时与 sys_user.pwd_version 比对，
/// 不一致说明改过密码 —— 返回 401 强制重新登录。
/// sys_user.pwd_version 查询结果缓存 60 秒，避免每个请求都打数据库。
/// 旧版 Token（无 ver 声明）兼容放行，直到自然过期。
/// </summary>
public class TokenVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public TokenVersionMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ISessionRegistry sessionRegistry)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var verClaim = context.User.FindFirst("ver")?.Value;
            var sidClaim = context.User.FindFirst("sid")?.Value;
            var idClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // 并发登录 / 最大会话数校验：仅校验带 sid 的新 Token（旧 Token 兼容放行）
            if (!string.IsNullOrEmpty(sidClaim) && long.TryParse(idClaim, out var sessionUserId)
                && !sessionRegistry.IsActive(sessionUserId, sidClaim))
            {
                await WriteUnauthorized(context, "账号已在其他终端登录，请重新登录");
                return;
            }

            // 有 ver 声明才校验（旧 Token 兼容放行）
            if (verClaim != null && long.TryParse(idClaim, out var userId))
            {
                var cacheKey = $"pwd_version:{userId}";
                if (!_cache.TryGetValue(cacheKey, out int currentVersion))
                {
                    currentVersion = await db.SysUsers.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => u.PwdVersion)
                        .FirstOrDefaultAsync();
                    _cache.Set(cacheKey, currentVersion, TimeSpan.FromSeconds(60));
                }

                if (int.TryParse(verClaim, out var tokenVersion) && tokenVersion < currentVersion)
                {
                    await WriteUnauthorized(context, "密码已修改，请重新登录");
                    return;
                }
            }
        }

        await _next(context);
    }

    /// <summary>统一输出 401 业务响应</summary>
    private static async Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new
        {
            code = 401,
            message,
            data = (object?)null
        });
        await context.Response.WriteAsync(body);
    }
}
