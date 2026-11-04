using System.Text.Json;
using Jsd.Api.Helpers;
using Jsd.Api.Services;

namespace Jsd.Api.Middlewares;

/// <summary>
/// 【F-22】Token 黑名单拦截中间件。
///
/// 位置：必须放在 UseAuthentication() 之后（此时 JWT 签名/有效期已校验通过、
///       HttpContext.User 已填充），UseAuthorization() 之前。
///
/// 逻辑：Token 签名合法 ≠ 会话有效。若该 Token 已被登出拉黑，
///       直接返回 401，且**不再进入任何 Controller**，杜绝"登出后旧 Token 仍可下单"。
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITokenBlacklistService blacklistService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var token = BearerTokenHelper.ReadToken(context);
            if (!string.IsNullOrEmpty(token) && await blacklistService.IsRevokedAsync(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    code = 401,
                    message = "登录已失效（该令牌已退出登录），请重新登录",
                    data = (object?)null
                }));
                return;
            }
        }

        await _next(context);
    }
}
