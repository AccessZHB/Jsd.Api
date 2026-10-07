namespace Jsd.Api.Attributes;

/// <summary>
/// 接口权限校验特性。
/// 挂在 Controller（类级）或 Action（方法级）上，声明该接口所需的权限标识；
/// 运行时由 <see cref="PermissionFilter"/> 全局过滤器解析并做服务端硬校验。
///
/// 权限标识必须与 sys_menu.permission 字段严格一致（如 system:user:add），
/// 且该条 sys_menu 记录需要通过 sys_role_menu 关联到当前用户的 role_id，接口才放行。
/// </summary>
/// <remarks>
/// 1. 类级与方法级都支持：Action 上标注了以 Action 为准；未标注则回退到 Controller 类级；
///    都没有则该接口不参与权限校验（沿用原有「仅登录即可访问」行为，保证存量接口零改动）。
/// 2. 支持传入多个权限标识（params），为「或」关系：命中任意一个即放行。
///    典型场景：某接口被多个业务入口共用（如 GET /sys/menu/all 同时被「菜单管理页」
///    和「角色分配菜单弹窗」调用），此时声明 [Permission(SysMenuPermissions.List, SysRolePermissions.Grant)]
///    可避免只挂单一权限而误伤另一入口。
///    该语义与前端 v-hasPermi / v-permission 传数组的「或」逻辑保持一致。
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PermissionAttribute : Attribute
{
    /// <summary>权限标识（对应 sys_menu.permission，可传多个，或关系）</summary>
    public PermissionAttribute(params string[] permissions)
    {
        Permissions = permissions ?? System.Array.Empty<string>();
    }

    /// <summary>所需权限标识集合（任意一个命中即视为有权）</summary>
    public string[] Permissions { get; }
}
