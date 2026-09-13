using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 登录校验：验证账号密码，成功返回 JWT Token 和用户基本信息
    /// </summary>
    /// <param name="dto">登录请求</param>
    /// <param name="clientIp">客户端IP（用于记录最后登录IP）</param>
    Task<ApiResponse<LoginResultDto>> LoginAsync(LoginDto dto, string? clientIp);
}
