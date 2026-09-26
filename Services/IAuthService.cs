using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 登录校验：验证码 → 账号状态 → 锁定检查 → BCrypt 密码 → 双令牌签发。
    /// 每次尝试（无论成败）均写入 sys_login_log；连续失败达到安全策略阈值自动锁定。
    /// </summary>
    /// <param name="dto">登录请求（含验证码会话）</param>
    /// <param name="clientIp">客户端IP（用于记录最后登录IP与登录日志）</param>
    /// <param name="userAgent">客户端 User-Agent（登录日志浏览器/系统解析）</param>
    Task<ApiResponse<LoginResultDto>> LoginAsync(LoginDto dto, string? clientIp, string? userAgent);
}
