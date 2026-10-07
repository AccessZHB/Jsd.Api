namespace Jsd.Api.Models.SysRole;

/// <summary>
/// 角色管理权限标识常量（与 sys_menu.permission 一一对应，见 sys_menu 中 角色管理(101) 下的按钮）。
/// 本常量与前端 v-permission / v-hasPermi 绑定同一字符串，后端 [Permission] 亦引用同一常量，
/// 保证「前端按钮显隐」与「后端接口拦截」口径一致。
/// </summary>
public static class SysRolePermissions
{
    /// <summary>角色列表（菜单）</summary>
    public const string List = "system:role:list";

    /// <summary>查询</summary>
    public const string Query = "system:role:query";

    /// <summary>新增</summary>
    public const string Add = "system:role:add";

    /// <summary>修改</summary>
    public const string Edit = "system:role:edit";

    /// <summary>删除</summary>
    public const string Remove = "system:role:remove";

    /// <summary>分配权限（角色管理页「分配菜单」按钮）</summary>
    public const string Grant = "system:role:grant";
}
