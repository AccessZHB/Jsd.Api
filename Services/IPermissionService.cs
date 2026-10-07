namespace Jsd.Api.Services;

/// <summary>
/// 后端权限校验服务：解析"当前登录用户"是否拥有某按钮级权限。
/// 权限来源：超级管理员 = 全部权限；普通角色 = 由其角色经 sys_role_menu 关联的 sys_menu.permission 推导。
/// 此前系统按钮级权限仅靠前端 v-permission 收敛，后端仅 [Authorize]，
/// 本服务用于在敏感接口（如重置密码）上做真正的服务端硬校验。
/// </summary>
public interface IPermissionService
{
    /// <summary>当前登录用户是否为超级管理员</summary>
    bool IsSuper { get; }

    /// <summary>
    /// 判断当前登录用户是否拥有指定权限标识。
    /// 超级管理员恒为 true；普通角色查其角色授权中的 permission 集合。
    /// </summary>
    Task<bool> HasPermissionAsync(string permission);

    /// <summary>
    /// 判断当前登录用户是否拥有"任意一个"权限标识（或关系）。
    /// 与 <see cref="HasPermissionAsync"/> 的区别：无论传入多少个标识，只查询一次角色权限集合，
    /// 避免逐条校验导致的重复数据库访问。
    /// 用于 [Permission] 特性声明多个权限的场景（如某接口被多个业务入口共用）。
    /// </summary>
    /// <param name="permissions">权限标识集合，任意一个命中即返回 true</param>
    Task<bool> HasAnyPermissionAsync(params string[] permissions);
}
