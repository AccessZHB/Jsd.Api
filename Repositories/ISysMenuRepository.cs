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
    /// 根据角色ID查询已授权的菜单ID列表（查 sys_role_menu 关联表）。
    /// </summary>
    Task<List<long>> GetMenuIdsByRoleIdAsync(long roleId);
}
