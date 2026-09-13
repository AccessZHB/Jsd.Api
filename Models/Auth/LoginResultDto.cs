namespace Jsd.Api.Models.Auth;

/// <summary>
/// 登录成功返回结果：JWT Token + 用户基本信息
/// </summary>
public class LoginResultDto
{
    /// <summary>JWT 访问令牌（前端后续请求放在 Header: Authorization: Bearer {token}）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>用户ID</summary>
    public long Id { get; set; }

    /// <summary>登录账号</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    public string RealName { get; set; } = string.Empty;

    /// <summary>头像URL</summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>角色ID</summary>
    public long? RoleId { get; set; }

    /// <summary>角色名称（如：超级管理员）</summary>
    public string? RoleName { get; set; }

    /// <summary>角色编码（如：admin）</summary>
    public string? RoleCode { get; set; }

    /// <summary>是否超级管理员：1-是 0-否</summary>
    public int IsSuper { get; set; }
}
