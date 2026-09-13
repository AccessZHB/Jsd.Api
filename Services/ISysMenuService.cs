using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysMenu;

namespace Jsd.Api.Services;

/// <summary>
/// 菜单服务接口
/// </summary>
public interface ISysMenuService
{
    /// <summary>
    /// 获取当前登录用户的菜单树（用于前端左侧导航栏）。
    /// 超级管理员返回全部菜单；其他角色按 sys_role_menu 授权返回。
    /// </summary>
    Task<ApiResponse<List<MenuTreeNodeDto>>> GetCurrentUserMenuTreeAsync();

    /// <summary>
    /// 根据 ID 获取菜单详情（用于修改表单回显）
    /// </summary>
    Task<ApiResponse<SysMenuDto>> GetByIdAsync(long id);

    /// <summary>
    /// 修改菜单
    /// </summary>
    Task<ApiResponse<object>> UpdateAsync(SysMenuUpdateDto dto);

    /// <summary>
    /// 根据 ID 删除菜单（含子菜单保护）
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    /// <summary>
    /// 批量删除菜单
    /// </summary>
    Task<ApiResponse<object>> DeleteBatchAsync(long[] ids);
}
