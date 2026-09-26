using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 后台管理员用户表 sys_user
/// 注意：本表与小程序会员表(member)物理隔离，业务逻辑严禁混用！
/// </summary>
[Table("sys_user")]
public class SysUser
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>登录账号（唯一）</summary>
    [Required]
    [StringLength(50)]
    [Column("username")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码（BCrypt 加密后的密文）</summary>
    [Required]
    [StringLength(128)]
    [Column("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    [StringLength(50)]
    [Column("real_name")]
    public string RealName { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    [StringLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    /// <summary>头像URL</summary>
    [StringLength(255)]
    [Column("avatar")]
    public string Avatar { get; set; } = string.Empty;

    /// <summary>角色ID，关联 sys_role.id</summary>
    [Column("role_id")]
    public long? RoleId { get; set; }

    /// <summary>是否超级管理员：1-是（绕过权限校验） 0-否</summary>
    [Column("is_super")]
    public int IsSuper { get; set; }

    /// <summary>状态：1-正常 0-禁用</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>最后登录时间</summary>
    [Column("last_login_time")]
    public DateTime? LastLoginTime { get; set; }

    /// <summary>最后登录IP</summary>
    [StringLength(50)]
    [Column("last_login_ip")]
    public string? LastLoginIp { get; set; }

    /// <summary>连续登录失败次数（达到安全策略阈值锁定账号，成功登录后清零；system_align.sql 补列）</summary>
    [Column("fail_count")]
    public int FailCount { get; set; }

    /// <summary>锁定截止时间（NULL-未锁定；达到失败阈值后 = 当前时间 + 锁定时长；system_align.sql 补列）</summary>
    [Column("lock_until")]
    public DateTime? LockUntil { get; set; }

    /// <summary>密码版本号（修改密码后 +1，JWT 中 ver 声明低于该值则强制下线；system_align.sql 补列）</summary>
    [Column("pwd_version")]
    public int PwdVersion { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>关联的角色（导航属性，用于 Include 出角色名称）</summary>
    [ForeignKey(nameof(RoleId))]
    public virtual SysRole? Role { get; set; }
}
