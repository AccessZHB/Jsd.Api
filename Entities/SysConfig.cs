using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 系统配置表 sys_config（键值对配置，登录安全策略存这里）
/// 本表由 system_align.sql 创建（主脚本 Jsd_order.sql 中没有），表结构以该脚本为准。
/// </summary>
[Table("sys_config")]
public class SysConfig
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>配置键（唯一），如 security.login</summary>
    [Required]
    [StringLength(100)]
    [Column("config_key")]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值（JSON 或纯文本）</summary>
    [Column("config_value")]
    public string? ConfigValue { get; set; }

    /// <summary>备注说明</summary>
    [StringLength(255)]
    [Column("remark")]
    public string Remark { get; set; } = string.Empty;

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
