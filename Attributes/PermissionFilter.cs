using System.Reflection;
using System.Text.RegularExpressions;
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
        // ============================================================
        // 【阶段六 · F-01】会员令牌与后台接口的硬隔离（在一切权限判断之前）
        //
        // 背景：本过滤器原先只对标注了 [Permission] 的接口做校验，而 45 个后台控制器
        //       （订单/应付/客户/供应商/角色/余额调整…）都没有标注 → 任何持会员令牌的人
        //       都能调用后台管理接口（已实测：会员令牌可读取全平台订单、应付、客户资料）。
        //
        // 现在的策略：**会员令牌只能访问会员端白名单**，白名单之外的请求一律 403，
        // 与是否标注 [Permission] 无关。管理员令牌（IsMember=false）走原有逻辑，行为不变。
        // ============================================================
        if (_currentUser.IsMember)
        {
            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            if (!IsMemberEndpoint(path))
            {
                _logger.LogWarning(
                    "越权拦截：会员 {MemberId} 尝试访问后台接口 {Method} {Path}",
                    _currentUser.MemberId, context.HttpContext.Request.Method, path);

                context.Result = new JsonResult(
                    ApiResponse<object>.Fail("无权访问该功能（会员账号不可调用后台接口）",
                        StatusCodes.Status403Forbidden))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            // 白名单内的会员接口：直接放行，**跳过 sys_role_menu 角色权限校验**。
            // 原因：会员令牌没有 role_id，若共享接口（如 /api/order/{id}/cancel）标了
            // [Permission("order:cancel")]，会员会被误拦；会员侧的安全边界是 Service 里的
            // "归属校验（BuyerId == 当前会员）"，不是后台角色权限。
            await next();
            return;
        }

        var required = ResolveRequiredPermissions(context.ActionDescriptor);

        // 未标注 [Permission] 的接口不校验权限（登录即可访问），保证存量接口零改动
        if (required is null || required.Length == 0)
        {
            await next();
            return;
        }

        // 多个标识为「或」关系：命中任意一个即放行
        if (await _permissionService.HasAnyPermissionAsync(required))
        {
            await next();
            return;
        }

        _logger.LogWarning(
            "权限校验失败：user={UserId}, role={RoleId}, 缺少权限={Permission}, 请求={Method} {Path}",
            _currentUser.UserId,
            _currentUser.RoleId?.ToString() ?? "null",
            string.Join(" 或 ", required),
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        context.Result = new JsonResult(
            ApiResponse<object>.Fail($"无权限访问该功能（缺少权限：{string.Join(" 或 ", required)}）", StatusCodes.Status403Forbidden))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    /// <summary>
    /// 会员端允许访问的接口白名单。
    ///
    /// 设计原则：**默认拒绝**（白名单之外一律 403），而不是"黑名单排除后台接口"——
    /// 后者每新增一个后台接口就漏一个。白名单依据是小程序 `Jsd.Mp/src/api/*.ts`
    /// 实际调用的全部路径（含动态路径 `/order/{id}/detail` 这类）。
    ///
    /// 新增会员端接口时，必须同步在这里登记，否则会员调用会被 403。
    /// </summary>
    private static readonly Regex[] MemberAllowedPaths =
    {
        // 会员身份：登录 / 验证码 / 登出 / 资料
        new(@"^/api/member(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 购物车（全部接口内部按 JWT member_id 隔离）
        new(@"^/api/cart(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 收货地址（内部按 memberId 过滤）
        new(@"^/api/address(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 钱包：充值 / 流水（流水按 memberId 过滤）
        new(@"^/api/wallet(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 余额概况：仅允许 GET /api/balance/{数字ID}（Service 内已加"只能查自己"校验）。
        // ⚠️ 不能放行整个 /api/balance 前缀 —— admin-adjust / freeze / deduct 是对公高危写接口。
        new(@"^/api/balance/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 会员本人重置密码（POST /api/customer/reset-password）。
        // ⚠️ 必须精确匹配：/api/customer/{id}/reset-password 是【后台】重置任意会员密码的接口，
        //    一旦被会员调用等于账号接管，绝不能放行。
        new(@"^/api/customer/reset-password$", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 小程序专用：物流轨迹、售后申请
        new(@"^/api/mp(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 商品浏览：详情 / SKU / 分类 / 轮播 / 公告
        new(@"^/api/prod-info(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/prod/(sku|category)(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/banner(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/cms(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 订单：仅这 4 个"无 id"的会员动作
        new(@"^/api/order/(my|status-count|pre-calculate|submit)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // 订单：带 id 的会员动作（详情 / 取消），id 必须是纯数字，
        // 且 Service 内部还会校验 BuyerId，双重保险。
        new(@"^/api/order/\d+/(detail|cancel)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>判断某个请求路径是否属于会员端白名单</summary>
    private static bool IsMemberEndpoint(string path)
        => MemberAllowedPaths.Any(r => r.IsMatch(path));

    /// <summary>
    /// 解析当前接口所需的权限标识：优先 Action 方法级，回退 Controller 类级。
    /// </summary>
    private static string[]? ResolveRequiredPermissions(ActionDescriptor descriptor)
    {
        if (descriptor is ControllerActionDescriptor actionDescriptor)
        {
            // 方法级优先
            var methodAttr = actionDescriptor.MethodInfo.GetCustomAttribute<PermissionAttribute>(inherit: false);
            if (methodAttr is not null)
            {
                return methodAttr.Permissions;
            }

            // 回退到 Controller 类级（inherit: true 支持继承基类 Controller 上的声明）
            return actionDescriptor.MethodInfo.DeclaringType?.GetCustomAttribute<PermissionAttribute>(inherit: true)?.Permissions;
        }

        // 非 Controller 场景兜底：从 EndpointMetadata 中取
        return descriptor.EndpointMetadata.OfType<PermissionAttribute>().FirstOrDefault()?.Permissions;
    }
}
