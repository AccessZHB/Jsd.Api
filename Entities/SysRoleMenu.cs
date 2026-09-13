using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 角色-菜单关联表 sys_role_menu（RBAC 多对多授权）
/// </summary>
[Table("sys_role_menu")]
public class SysRoleMenu
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>角色ID，关联 sys_role.id</summary>
    [Column("role_id")]
    public long RoleId { get; set; }

    /// <summary>菜单ID，关联 sys_menu.id</summary>
    [Column("menu_id")]
    public long MenuId { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>关联的角色（导航属性）</summary>
    [ForeignKey(nameof(RoleId))]
    public virtual SysRole? Role { get; set; }

    /// <summary>关联的菜单（导航属性）</summary>
    [ForeignKey(nameof(MenuId))]
    public virtual SysMenu? Menu { get; set; }
}
