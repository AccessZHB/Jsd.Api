using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 系统配置表 sys_config（系统管理 - 系统配置模块）
///
/// ⚠️【表结构以 Jsd_order.sql（约 2273 行）与真实库为准】
///   现有库是完整 12 列版；system_align.sql 里那份 6 列的 CREATE TABLE IF NOT EXISTS
///   是历史遗留的精简版（表已存在时不会执行），不要拿它当表结构依据。
///   1) config_value：VARCHAR(500) NOT NULL DEFAULT ''，实体用非空 string（不要写 null）。
///   2) config_type：CHAR(1)，'Y'-系统内置（不可删除） 'N'-自定义，默认 'N'。
///   3) status：TINYINT，0-正常 1-停用。
///   4) create_by / update_by：VARCHAR(64) 账号名（"admin"），不是 BIGINT 用户ID。
///   5) create_time / update_time 由数据库默认值与 ON UPDATE 维护，标注 Computed 避免 EF 覆盖。
/// </summary>
[Table("sys_config")]
public class SysConfig
{
    /// <summary>配置主键</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>配置名称（中文展示名，如「登录安全策略」）</summary>
    [Required]
    [StringLength(100)]
    [Column("config_name")]
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>配置键名（唯一标识，代码中通过此键取值，如 security.login）</summary>
    [Required]
    [StringLength(100)]
    [Column("config_key")]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值（统一存字符串，由调用方自行转换类型）</summary>
    [Required]
    [StringLength(500)]
    [Column("config_value")]
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置类型：Y-系统内置（不可删除） N-自定义</summary>
    [Required]
    [StringLength(1)]
    [Column("config_type")]
    public string ConfigType { get; set; } = "N";

    /// <summary>排序（列表升序展示）</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>创建者账号</summary>
    [StringLength(64)]
    [Column("create_by")]
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>创建时间（数据库 CURRENT_TIMESTAMP）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新者账号</summary>
    [StringLength(64)]
    [Column("update_by")]
    public string UpdateBy { get; set; } = string.Empty;

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP，标注 Computed 避免 EF 覆盖）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }
}
