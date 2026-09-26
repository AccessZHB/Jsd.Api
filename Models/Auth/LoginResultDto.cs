namespace Jsd.Api.Models.Auth;

/// <summary>
/// 登录成功返回结果：JWT Token + 用户基本信息
/// </summary>
public class LoginResultDto
{
    /// <summary>JWT 访问令牌（前端后续请求放在 Header: Authorization: Bearer {token}；与旧前端兼容字段）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>访问令牌（与 Token 同值；系统管理模块新契约字段，前端升级后改用本字段）</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>刷新令牌（access 过期前调 POST /api/auth/refresh 换新令牌对）</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（秒）</summary>
    public int ExpiresIn { get; set; }

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
