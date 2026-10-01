using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 菜单仓储接口
/// </summary>
public interface ISysMenuRepository : IRepository<SysMenu>
{
    /// <summary>
    /// 查询所有可见菜单（状态正常 + 显示中 + 类型为目录/菜单）。
    /// 超级管理员拥有全部菜单，直接使用此结果。
    /// </summary>
    Task<List<SysMenu>> GetAllVisibleMenusAsync();

    /// <summary>
    /// 查询全部菜单（不做任何过滤，含隐藏/停用/按钮，菜单管理页专用）。
    /// </summary>
    Task<List<SysMenu>> GetAllMenusAsync();

    /// <summary>
    /// 根据角色ID查询已授权的菜单ID列表（查 sys_role_menu 关联表）。
    /// </summary>
    Task<List<long>> GetMenuIdsByRoleIdAsync(long roleId);

    /// <summary>
    /// 根据角色ID查询其拥有的全部权限标识（button 级 permission 字符串）。
    /// 通过 sys_role_menu 关联 sys_menu，取其中非空的 permission 字段并去重。
    /// 供 IPermissionService 做后端按权限的硬校验。
    /// </summary>
    Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
}
