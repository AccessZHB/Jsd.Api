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
}
