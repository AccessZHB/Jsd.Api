using System.Linq;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 后端权限校验服务实现。
/// 依赖 CurrentUserService（从 JWT 声明读取 is_super / role_id）与 ISysMenuRepository（角色→权限推导）。
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly CurrentUserService _currentUser;
    private readonly ISysMenuRepository _menuRepository;

    public PermissionService(CurrentUserService currentUser, ISysMenuRepository menuRepository)
    {
        _currentUser = currentUser;
        _menuRepository = menuRepository;
    }

    /// <summary>当前登录用户是否为超级管理员（JWT 中 is_super 声明为 "1"）</summary>
    public bool IsSuper => _currentUser.IsSuper;

    /// <summary>
    /// 判断当前登录用户是否拥有指定权限。
    /// 超级管理员直接返回 true；普通角色查其角色已授权的 permission 集合。
    /// 未登录 / 无角色 / 角色无任何授权时返回 false。
    /// </summary>
    public async Task<bool> HasPermissionAsync(string permission)
    {
        // 超级管理员拥有全部权限
        if (_currentUser.IsSuper)
        {
            return true;
        }

        // 普通用户必须有绑定角色
        var roleId = _currentUser.RoleId;
        if (roleId == null || roleId <= 0)
        {
            return false;
        }

        var permissions = await _menuRepository.GetPermissionsByRoleIdAsync(roleId.Value);
        return permissions.Contains(permission);
    }

    /// <summary>
    /// 判断当前用户是否拥有"任意一个"权限标识（或关系）。
    /// 只查询一次角色权限集合再取交集，避免逐条校验导致的重复数据库访问。
    /// 用于 [Permission] 特性声明多个权限的场景（接口被多个业务入口共用）。
    /// </summary>
    public async Task<bool> HasAnyPermissionAsync(params string[] permissions)
    {
        if (permissions is null || permissions.Length == 0)
        {
            return false;
        }

        // 超级管理员拥有全部权限
        if (_currentUser.IsSuper)
        {
            return true;
        }

        // 普通用户必须有绑定角色
        var roleId = _currentUser.RoleId;
        if (roleId == null || roleId <= 0)
        {
            return false;
        }

        var owned = await _menuRepository.GetPermissionsByRoleIdAsync(roleId.Value);
        return permissions.Any(owned.Contains);
    }
}
