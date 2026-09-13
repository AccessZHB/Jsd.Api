using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 后台菜单表 sys_menu（树形结构：目录 / 菜单 / 按钮）
/// </summary>
[Table("sys_menu")]
public class SysMenu
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>父级菜单ID（0 = 顶级菜单）</summary>
    [Column("parent_id")]
    public long ParentId { get; set; }

    /// <summary>菜单名称（如：订单管理 / 商品列表）</summary>
    [Required]
    [StringLength(50)]
    [Column("menu_name")]
    public string MenuName { get; set; } = string.Empty;

    /// <summary>前端路由路径（如：/order/list）</summary>
    [StringLength(100)]
    [Column("path")]
    public string? Path { get; set; }

    /// <summary>前端组件路径（如：system/user/index）</summary>
    [StringLength(100)]
    [Column("component")]
    public string? Component { get; set; }

    /// <summary>菜单图标</summary>
    [StringLength(50)]
    [Column("icon")]
    public string? Icon { get; set; }

    /// <summary>排序号（越小越靠前）</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    [Column("menu_type")]
    public int MenuType { get; set; } = 1;

    /// <summary>权限标识（如 system:user:list / order:ship）</summary>
    [StringLength(100)]
    [Column("permission")]
    public string? Permission { get; set; }

    /// <summary>是否显示：1-显示 0-隐藏</summary>
    [Column("visible")]
    public int Visible { get; set; } = 1;

    /// <summary>状态：1-正常 0-停用</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
