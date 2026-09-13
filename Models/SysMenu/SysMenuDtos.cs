using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.SysMenu;

/// <summary>
/// 菜单详情 DTO（详情接口返回，用于修改表单回显）
/// </summary>
public class SysMenuDto
{
    public long Id { get; set; }

    /// <summary>父级菜单ID（0=顶级）</summary>
    public long ParentId { get; set; }

    /// <summary>菜单名称</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>前端路由路径</summary>
    public string? Path { get; set; }

    /// <summary>前端组件路径</summary>
    public string? Component { get; set; }

    /// <summary>菜单图标</summary>
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    public int MenuType { get; set; }

    /// <summary>权限标识</summary>
    public string? Permission { get; set; }

    /// <summary>是否显示：1-显示 0-隐藏</summary>
    public int Visible { get; set; }

    /// <summary>状态：1-正常 0-停用</summary>
    public int Status { get; set; }
}

/// <summary>
/// 修改菜单请求 DTO
/// </summary>
public class SysMenuUpdateDto
{
    /// <summary>菜单ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "菜单ID不合法")]
    public long Id { get; set; }

    /// <summary>父级菜单ID（0=顶级）</summary>
    [Range(0, long.MaxValue, ErrorMessage = "父级菜单ID不合法")]
    public long ParentId { get; set; }

    /// <summary>菜单名称</summary>
    [Required(ErrorMessage = "菜单名称不能为空")]
    [StringLength(50, ErrorMessage = "菜单名称最长50个字符")]
    public string MenuName { get; set; } = string.Empty;

    /// <summary>前端路由路径</summary>
    [StringLength(100)]
    public string? Path { get; set; }

    /// <summary>前端组件路径</summary>
    [StringLength(100)]
    public string? Component { get; set; }

    /// <summary>菜单图标</summary>
    [StringLength(50)]
    public string? Icon { get; set; }

    /// <summary>排序号（越小越靠前）</summary>
    public int SortOrder { get; set; }

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    [Range(1, 3, ErrorMessage = "菜单类型只能是1-目录/2-菜单/3-按钮")]
    public int MenuType { get; set; } = 1;

    /// <summary>权限标识（如 system:user:list）</summary>
    [StringLength(100)]
    public string? Permission { get; set; }

    /// <summary>是否显示：1-显示 0-隐藏</summary>
    public int Visible { get; set; } = 1;

    /// <summary>状态：1-正常 0-停用</summary>
    public int Status { get; set; } = 1;
}
