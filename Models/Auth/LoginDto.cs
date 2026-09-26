using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Auth;

/// <summary>
/// 登录请求 DTO
/// </summary>
public class LoginDto
{
    /// <summary>登录账号</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码（明文传输，服务端用 BCrypt 校验；生产环境建议 HTTPS）</summary>
    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; set; } = string.Empty;

    /// <summary>验证码会话ID（安全策略开启验证码时必传，来自 POST /api/auth/captcha）</summary>
    public string? CaptchaKey { get; set; }

    /// <summary>验证码（安全策略开启验证码时必传，一次性消耗）</summary>
    public string? CaptchaCode { get; set; }
}
