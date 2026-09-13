using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.SysUser;

/// <summary>
/// 用户列表项 DTO（分页查询返回，密码不返回给前端）
/// </summary>
public class SysUserListDto
{
    public long Id { get; set; }

    /// <summary>登录账号</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    public string RealName { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string? Phone { get; set; }

    /// <summary>头像URL</summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>角色ID</summary>
    public long? RoleId { get; set; }

    /// <summary>角色名称（来自 sys_role 表，AutoMapper 从导航属性 Role 取）</summary>
    public string? RoleName { get; set; }

    /// <summary>是否超级管理员：1-是 0-否</summary>
    public int IsSuper { get; set; }

    /// <summary>状态：1-正常 0-禁用</summary>
    public int Status { get; set; }

    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 新增用户请求 DTO
/// </summary>
public class SysUserCreateDto
{
    /// <summary>登录账号</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, ErrorMessage = "用户名最长50个字符")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码（明文，服务端会 BCrypt 加密后入库）</summary>
    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "密码长度需为6~50个字符")]
    public string Password { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    [StringLength(50)]
    public string RealName { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>角色ID</summary>
    public long? RoleId { get; set; }

    /// <summary>是否超级管理员：1-是 0-否（一般新建用户给0）</summary>
    public int IsSuper { get; set; } = 0;

    /// <summary>状态：1-正常 0-禁用</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 修改用户请求 DTO
/// 注意：不支持修改密码（改密码建议走单独的"重置密码"接口，更安全）
/// </summary>
public class SysUserUpdateDto
{
    /// <summary>用户ID（必传，Range 校验主键合法性）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "用户ID不合法")]
    public long Id { get; set; }

    /// <summary>登录账号</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, ErrorMessage = "用户名最长50个字符")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    [StringLength(50)]
    public string RealName { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>角色ID</summary>
    public long? RoleId { get; set; }

    /// <summary>是否超级管理员：1-是 0-否</summary>
    public int IsSuper { get; set; }

    /// <summary>状态：1-正常 0-禁用</summary>
    public int Status { get; set; } = 1;
}
