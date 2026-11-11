namespace Jsd.Api.Attributes;

/// <summary>
/// 显式声明「本接口有意不做角色权限校验」。
///
/// 背景：<see cref="PermissionFilter"/> 已改为「写操作默认拒绝」——
/// 未标注 <see cref="PermissionAttribute"/> 的 POST/PUT/PATCH/DELETE 请求一律 403，
/// 从机制上杜绝"新增接口忘记加权限导致裸奔"。
///
/// 确实不需要角色权限的写接口（例如会员自助保存地址、登录页提交），
/// 必须显式标注本特性并写明原因，让「放行」成为一次可见的、可审计的决定，
/// 而不是「忘记加特性」的默认结果。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class PermissionFreeAttribute : Attribute
{
    /// <summary>放行原因（必填，便于代码审查时判断是否合理）</summary>
    public string Reason { get; }

    public PermissionFreeAttribute(string reason)
    {
        Reason = reason;
    }
}
