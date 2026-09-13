using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysRole;

namespace Jsd.Api.Services;

/// <summary>
/// 角色服务接口
/// </summary>
public interface ISysRoleService
{
    /// <summary>
    /// 获取角色列表（用于下拉选择 / 角色管理页）
    /// </summary>
    Task<ApiResponse<List<SysRoleDto>>> GetListAsync();

    /// <summary>
    /// 根据 ID 获取角色详情
    /// </summary>
    Task<ApiResponse<SysRoleDto>> GetByIdAsync(long id);

    /// <summary>
    /// 修改角色
    /// </summary>
    Task<ApiResponse<object>> UpdateAsync(SysRoleUpdateDto dto);

    /// <summary>
    /// 根据 ID 删除角色（含业务保护）
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    /// <summary>
    /// 批量删除角色
    /// </summary>
    Task<ApiResponse<object>> DeleteBatchAsync(long[] ids);

    /// <summary>
    /// 给角色分配菜单权限（全量替换 sys_role_menu）
    /// </summary>
    Task<ApiResponse<object>> AssignMenusAsync(SysRoleAuthDto dto);

    /// <summary>
    /// 获取角色已授权的菜单ID集合（用于分配菜单弹窗回显）
    /// </summary>
    Task<ApiResponse<List<long>>> GetRoleMenuIdsAsync(long roleId);
}
