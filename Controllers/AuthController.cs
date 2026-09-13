using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 认证接口（登录 / 登出）
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 管理员登录
    /// 请求体：{ "userName": "admin", "password": "admin123" }
    /// 成功返回：JWT Token + 用户基本信息
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]  // 登录接口不需要 Token
    public async Task<ApiResponse<LoginResultDto>> Login([FromBody] LoginDto dto)
    {
        // 获取客户端IP，用于记录最后登录IP
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        return await _authService.LoginAsync(dto, clientIp);
    }

    /// <summary>
    /// 登出（JWT 无状态，前端清除本地 Token 即可；此接口保留用于日志/后续黑名单扩展）
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public ApiResponse<object> Logout()
    {
        return ApiResponse<object>.Success(new { }, "登出成功");
    }
}
