using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jsd.Api.Attributes;

/// <summary>
/// 【阶段七】超级管理员专用接口特性。
///
/// 适用场景：**没有对应菜单权限码、但又极度危险**的操作，例如
///   - 余额调整 / 冻结 / 直接扣款（`/api/balance/admin-adjust|freeze|unfreeze|deduct`）
///   - 后台代客充值 / 充值退款
///   - 后台重置任意会员密码（账号接管风险）
///
/// 与 <see cref="PermissionAttribute"/> 的区别：
///   - `[Permission("order:cancel")]` 走 `sys_role_menu` 角色授权，可授予任意角色；
///   - `[SuperAdminOnly]` 只看 JWT 的 `is_super=1`，**任何角色管理员都无权**，只有超管放行。
///
/// 两者可叠加使用；本特性在 Action 执行前拦截，命中即 403，不进入业务逻辑。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class SuperAdminOnlyAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>审计日志里标识该操作，便于事后追溯</summary>
    public string? Operation { get; set; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();

        // 会员令牌一律拒绝（即便 is_super 声明被伪造，会员令牌也不会带该声明）
        if (currentUser.IsMember || !currentUser.IsSuper)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<SuperAdminOnlyAttribute>>();

            logger.LogWarning(
                "超管专属接口拦截：user={UserId} role={RoleId} isSuper={IsSuper} op={Operation} {Method} {Path}",
                currentUser.UserId, currentUser.RoleId, currentUser.IsSuper,
                Operation ?? "-", context.HttpContext.Request.Method, context.HttpContext.Request.Path);

            context.Result = new JsonResult(
                ApiResponse<object>.Fail("该操作仅限超级管理员执行", StatusCodes.Status403Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
