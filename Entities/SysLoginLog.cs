using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 登录日志表 sys_login_log
/// 记录后台管理员的所有登录尝试（成功/失败），每次尝试均写入一条。
/// 表结构以 Jsd_order.sql（约 2011 行）为准。
/// 注意：member_id 字段名虽叫 member，实际关联的是 sys_user（后台管理员），
/// 与小程序会员表 mem_member 无关 —— 文档原文如此，勿改关联。
/// </summary>
[Table("sys_login_log")]
public class SysLoginLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>管理员ID，关联 sys_user 表（失败且账号不存在时为 NULL）</summary>
    [Column("member_id")]
    public long? MemberId { get; set; }

    /// <summary>管理员用户名（登录尝试时输入的账号）</summary>
    [StringLength(50)]
    [Column("member_name")]
    public string MemberName { get; set; } = string.Empty;

    /// <summary>登录方式：1-账号密码 2-微信授权 3-短信验证码</summary>
    [Column("login_type")]
    public int LoginType { get; set; } = 1;

    /// <summary>登录IP地址</summary>
    [StringLength(50)]
    [Column("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>客户端User-Agent</summary>
    [StringLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>浏览器类型（自动解析）</summary>
    [StringLength(50)]
    [Column("browser")]
    public string Browser { get; set; } = string.Empty;

    /// <summary>操作系统（自动解析）</summary>
    [StringLength(50)]
    [Column("os")]
    public string Os { get; set; } = string.Empty;

    /// <summary>登录地点（根据IP解析省/市，解析失败留空）</summary>
    [StringLength(100)]
    [Column("login_location")]
    public string LoginLocation { get; set; } = string.Empty;

    /// <summary>登录状态：1-成功 0-失败</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>失败原因（密码错误/账号已禁用/超过锁定时间/验证码错误）</summary>
    [StringLength(255)]
    [Column("fail_reason")]
    public string FailReason { get; set; } = string.Empty;

    /// <summary>登录时间（成功时写入）</summary>
    [Column("login_time")]
    public DateTime? LoginTime { get; set; }

    /// <summary>记录创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
