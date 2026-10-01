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
/// 类级与方法级都支持：Action 上标注了以 Action 为准；未标注则回退到 Controller 类级；
/// 都没有则该接口不参与权限校验（沿用原有「仅登录即可访问」行为，保证存量接口零改动）。
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PermissionAttribute : Attribute
{
    /// <summary>权限标识（对应 sys_menu.permission，如 system:user:add / order:logistics:query）</summary>
    public PermissionAttribute(string permission)
    {
        Permission = permission;
    }

    /// <summary>所需权限标识</summary>
    public string Permission { get; }
}
