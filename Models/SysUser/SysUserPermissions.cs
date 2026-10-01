namespace Jsd.Api.Models.SysUser;

/// <summary>
/// 系统用户（管理员）管理权限标识常量（与 sys_menu.permission 一一对应）。
/// 本项目后端只挂 [Authorize] 做登录校验，按钮级权限此前由前端 v-permission 消费这些标识，
/// 现新增 IPermissionService 在后端做按权限的硬校验（见 SysUserService.ResetPasswordAsync）。
///
/// 说明：Jsd_order.sql 中已存在 管理员管理(100) 下按钮
/// 查询(10000, system:user:query) / 新增(10001, system:user:add) /
/// 修改(10002, system:user:edit) / 删除(10003, system:user:remove)，本常量与其保持一致。
/// 重置密码复用"修改用户"权限（能编辑账号即能重置其密码），避免新增独立权限带来的
/// 菜单种子与角色授权迁移成本。
/// </summary>
public static class SysUserPermissions
{
    /// <summary>管理员列表（菜单）</summary>
    public const string List = "system:user:list";

    /// <summary>查询</summary>
    public const string Query = "system:user:query";

    /// <summary>新增</summary>
    public const string Add = "system:user:add";

    /// <summary>修改（重置密码复用此权限）</summary>
    public const string Edit = "system:user:edit";

    /// <summary>删除</summary>
    public const string Remove = "system:user:remove";
}
