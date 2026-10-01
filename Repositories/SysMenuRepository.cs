using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 菜单仓储实现
/// </summary>
public class SysMenuRepository : Repository<SysMenu>, ISysMenuRepository
{
    public SysMenuRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 查询所有可见菜单（用于左侧导航栏）：
    /// 只取 目录(1) 和 菜单(2)，按钮(3) 是权限标识，不在导航栏显示。
    /// </summary>
    public async Task<List<SysMenu>> GetAllVisibleMenusAsync()
    {
        return await Db.SysMenus
            .AsNoTracking()
            .Where(m => m.Status == 1
                     && m.Visible == 1
                     && (m.MenuType == 1 || m.MenuType == 2))
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 查询全部菜单（不过滤，菜单管理页专用：隐藏/停用的菜单也要能看到并管理）
    /// </summary>
    public async Task<List<SysMenu>> GetAllMenusAsync()
    {
        return await Db.SysMenus
            .AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 查询某角色已授权的菜单ID集合
    /// </summary>
    public async Task<List<long>> GetMenuIdsByRoleIdAsync(long roleId)
    {
        return await Db.SysRoleMenus
            .AsNoTracking()
            .Where(rm => rm.RoleId == roleId)
            .Select(rm => rm.MenuId)
            .ToListAsync();
    }

    /// <summary>
    /// 查询某角色拥有的全部权限标识（button 级 permission）。
    /// 关联 sys_menu 后取非空的 permission 并去重。
    /// </summary>
    public async Task<List<string>> GetPermissionsByRoleIdAsync(long roleId)
    {
        return await Db.SysRoleMenus
            .AsNoTracking()
            .Where(rm => rm.RoleId == roleId)
            .Join(Db.SysMenus,
                rm => rm.MenuId,
                m => m.Id,
                (rm, m) => m.Permission)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .Distinct()
            .ToListAsync();
    }
}
