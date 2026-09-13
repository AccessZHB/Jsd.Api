namespace Jsd.Api.Models.SysMenu;

/// <summary>
/// 菜单树节点 DTO（返回给前端，用于渲染左侧导航栏 / 动态路由）
/// </summary>
public class MenuTreeNodeDto
{
    /// <summary>菜单ID</summary>
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

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    public int MenuType { get; set; }

    /// <summary>权限标识</summary>
    public string? Permission { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>子菜单（递归树形结构）</summary>
    public List<MenuTreeNodeDto> Children { get; set; } = new();
}
