namespace Jsd.Api.Models.SysMenu;

/// <summary>
/// 菜单管理权限标识常量（与 sys_menu.permission 一一对应，见 sys_menu 中 菜单管理(102) 下的按钮）。
/// </summary>
public static class SysMenuPermissions
{
    /// <summary>菜单列表（菜单）</summary>
    public const string List = "system:menu:list";

    /// <summary>查询</summary>
    public const string Query = "system:menu:query";

    /// <summary>新增</summary>
    public const string Add = "system:menu:add";

    /// <summary>修改</summary>
    public const string Edit = "system:menu:edit";

    /// <summary>删除</summary>
    public const string Remove = "system:menu:remove";
}
