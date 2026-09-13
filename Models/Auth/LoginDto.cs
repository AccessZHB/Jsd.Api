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
}
