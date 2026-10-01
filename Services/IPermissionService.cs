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
}
