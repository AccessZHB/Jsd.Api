using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 后台角色表 sys_role
/// （如：超级管理员 / 运营专员 / 仓库管理员 / 财务）
/// </summary>
[Table("sys_role")]
public class SysRole
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>角色名称（如：超级管理员）</summary>
    [Required]
    [StringLength(50)]
    [Column("role_name")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（唯一，如 admin / operation / warehouse）</summary>
    [Required]
    [StringLength(30)]
    [Column("role_code")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>数据权限：1-全部 2-本部门 3-仅本人</summary>
    [Column("data_scope")]
    public int DataScope { get; set; } = 1;

    /// <summary>状态：1-启用 0-停用</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成，代码中不要赋值）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>该角色下的用户列表（导航属性）</summary>
    public virtual ICollection<SysUser> Users { get; set; } = new List<SysUser>();
}
