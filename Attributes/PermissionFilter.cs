using System.Reflection;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jsd.Api.Attributes;

/// <summary>
/// 全局接口权限校验过滤器。
///
/// 工作流程（严格基于 sys_role_menu + sys_menu 表结构）：
/// 1. 解析当前 Action（或其 Controller 类）上的 <see cref="PermissionAttribute"/>，取所需权限标识；
/// 2. 未标注特性的接口直接放行（保持存量接口行为不变，也保证登录/验证码等公共接口不被拦截）；
/// 3. 已标注的接口，交由 IPermissionService 判定：
///    - 超级管理员（JWT 中 is_super="1"）直接放行；
///    - 否则取 JWT 中的 role_id，关联 sys_role_menu → sys_menu，
///      判断该角色是否已分配指定 permission 标识；
/// 4. 无权限 → 返回统一 403 JSON（{ code:403, message:"无权限访问该功能", data:null }）。
/// </summary>
/// <remarks>
/// 为什么用 ActionFilter 而不是中间件：中间件位于 MVC 路由之前，拿不到「具体要执行哪个 Action」，
/// 无法读取 Action 上的 [Permission] 特性。因此必须走 IAsyncActionFilter（或 IAsyncAuthorizationFilter）。
/// </remarks>
public class PermissionFilter : IAsyncActionFilter
{
    private readonly IPermissionService _permissionService;
    private readonly CurrentUserService _currentUser;
    private readonly ILogger<PermissionFilter> _logger;

    public PermissionFilter(
        IPermissionService permissionService,
        CurrentUserService currentUser,
        ILogger<PermissionFilter> logger)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var required = ResolveRequiredPermission(context.ActionDescriptor);

        // 未标注 [Permission] 的接口不校验权限（登录即可访问），保证存量接口零改动
        if (required is null)
        {
            await next();
            return;
        }

        if (await _permissionService.HasPermissionAsync(required))
        {
            await next();
            return;
        }

        _logger.LogWarning(
            "权限校验失败：user={UserId}, role={RoleId}, 缺少权限={Permission}, 请求={Method} {Path}",
            _currentUser.UserId,
            _currentUser.RoleId?.ToString() ?? "null",
            required,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        context.Result = new JsonResult(
            ApiResponse<object>.Fail($"无权限访问该功能（缺少权限：{required}）", StatusCodes.Status403Forbidden))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    /// <summary>
    /// 解析当前接口所需的权限标识：优先 Action 方法级，回退 Controller 类级。
    /// </summary>
    private static string? ResolveRequiredPermission(ActionDescriptor descriptor)
    {
        if (descriptor is ControllerActionDescriptor actionDescriptor)
        {
            // 方法级优先
            var methodAttr = actionDescriptor.MethodInfo.GetCustomAttribute<PermissionAttribute>(inherit: false);
            if (methodAttr is not null)
            {
                return methodAttr.Permission;
            }

            // 回退到 Controller 类级（inherit: true 支持继承基类 Controller 上的声明）
            return actionDescriptor.MethodInfo.DeclaringType?.GetCustomAttribute<PermissionAttribute>(inherit: true)?.Permission;
        }

        // 非 Controller 场景兜底：从 EndpointMetadata 中取
        return descriptor.EndpointMetadata.OfType<PermissionAttribute>().FirstOrDefault()?.Permission;
    }
}
