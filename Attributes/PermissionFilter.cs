using System.Reflection;
using System.Text.RegularExpressions;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
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
    private readonly bool _denyUnpermissionedWrites;

    public PermissionFilter(
        IPermissionService permissionService,
        CurrentUserService currentUser,
        ILogger<PermissionFilter> logger,
        IConfiguration configuration)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
        _logger = logger;
        // 写操作默认拒绝的开关（默认开启）。若某次发版漏配权限导致业务受阻，
        // 可通过配置 Security:DenyUnpermissionedWrites=false 立即回滚，无需改代码重新发布。
        _denyUnpermissionedWrites = configuration.GetValue("Security:DenyUnpermissionedWrites", true);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        // ============================================================
        // 【阶段六 · F-01】会员令牌与后台接口的硬隔离（在一切权限判断之前）
        //
        // 背景：本过滤器原先只对标注了 [Permission] 的接口做校验，而 45 个后台控制器
        //       （订单/应付/客户/供应商/角色/余额调整…）都没有标注 → 任何持会员令牌的人
        //       都能调用后台管理接口（已实测：会员令牌可读取全平台订单、应付、客户资料）。
        //
        // 现在的策略：**会员令牌只能访问会员端白名单**，白名单之外的请求一律 403，
        // 与是否标注 [Permission] 无关。管理员令牌（IsMember=false）走原有逻辑，行为不变。
        //
        // 【P0-01 修复】白名单原先用「控制器前缀」放行整个控制器，而 ProdSkuController
        //       （/api/prod/sku）、ProdInfoController（/api/prod-info）、BannerController、
        //       CmsAnnouncementController 的写接口没有任何 [Permission]，
        //       导致会员可直接 POST/PUT 改 SKU 价格、删商品、发公告。
        //       现在追加「方法约束」：对「商品/横幅/公告」这类后台浏览前缀，
        //       会员令牌只允许只读方法（GET/HEAD/OPTIONS），写方法一律 403。
        // ============================================================
        if (_currentUser.IsMember)
        {
            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            var allowed = IsMemberEndpoint(path) && IsMemberSafeMethod(path, method);

            if (!allowed)
            {
                _logger.LogWarning(
                    "越权拦截：会员 {MemberId} 尝试访问后台接口 {Method} {Path}",
                    _currentUser.MemberId, method, path);

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

        // ============================================================
        // [AllowAnonymous] 的公开接口：直接放行，不参与「写操作默认拒绝」。
        //
        // ⚠️ 必须放在默认拒绝逻辑之前。否则登录 POST /api/auth/login、
        //    验证码 POST /api/auth/captcha、会员登录、支付/充值回调
        //    （都是 [AllowAnonymous] 且没有 [Permission]）会被当作
        //    "未标权限的写接口"直接 403，表现为：
        //    登录页提示「登录需验证码，但验证码服务不可用」、登录按钮无响应、
        //    第三方支付回调全部失败。
        // ============================================================
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next();
            return;
        }

        var required = ResolveRequiredPermissions(context.ActionDescriptor);
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;

        // 显式声明「有意不做角色权限校验」的接口直接放行（放行是一次可见的决定，而非默认结果）
        if (IsPermissionFree(actionDescriptor))
        {
            await next();
            return;
        }

        // 未标注 [Permission] 的接口
        if (required is null || required.Length == 0)
        {
            // 【P0 机制修复】写操作默认拒绝：
            //   只要没有 [Permission] / [PermissionFree]，POST/PUT/PATCH/DELETE 一律 403。
            //   这样"新增接口忘记加权限"的默认结果变成「不可用」而不是「全平台可写」。
            var isWrite = HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
                          || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

            if (!isWrite || !_denyUnpermissionedWrites)
            {
                await next();
                return;
            }

            // 迁移期豁免名单：这些控制器尚未逐个补 [Permission]，先放行并打告警日志。
            // 每补完一个控制器就把它从名单里删掉；新控制器**不在名单内** → 天然默认拒绝。
            if (actionDescriptor is not null && LegacyUnpermissionedControllers.Contains(actionDescriptor.ControllerName))
            {
                _logger.LogWarning(
                    "安全告警：未标权限的写接口被调用 {Method} {Path}（控制器 {Controller} 仍在迁移豁免名单中，请尽快补 [Permission]）",
                    method, context.HttpContext.Request.Path.Value, actionDescriptor.ControllerName);
                await next();
                return;
            }

            _logger.LogWarning(
                "安全拦截：未标权限的写接口被拒绝 {Method} {Path}（控制器 {Controller}）",
                method, context.HttpContext.Request.Path.Value,
                actionDescriptor?.ControllerName ?? "unknown");

            context.Result = new JsonResult(
                ApiResponse<object>.Fail("该功能未配置权限标识，已按安全策略拒绝访问", StatusCodes.Status403Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
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
    /// 会员白名单中「只读」的路径段：商品 / SKU / 分类 / 轮播 / 公告 / 余额概况。
    ///
    /// 【P0-01 修复】这些控制器与后台管理控制器共用同一个前缀，且写接口没有 [Permission]。
    /// 若只按路径放行，会员令牌可直接 `PUT /api/prod/sku` 把价格改成 0.01 元后下单。
    /// 因此对下列前缀追加方法约束：会员令牌只允许 GET/HEAD/OPTIONS。
    /// </summary>
    private static readonly Regex[] MemberReadOnlyPaths =
    {
        new(@"^/api/prod-info(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/prod/(sku|category)(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/banner(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/cms(/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/balance/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>会员令牌访问该路径时，方法是否安全（只读路径只允许安全方法）</summary>
    private static bool IsMemberSafeMethod(string path, string method)
    {
        var isSafeMethod = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        if (isSafeMethod)
        {
            return true;
        }

        // 命中了只读路径却使用写方法 → 判定为越权（典型场景：会员改价、删商品、发公告）
        return !MemberReadOnlyPaths.Any(r => r.IsMatch(path));
    }

    /// <summary>
    /// 迁移期豁免名单：这些控制器的写接口尚未补齐 [Permission]，暂时放行但会打安全告警。
    ///
    /// 迁移规则：
    ///   1) 每给一个控制器补完 [Permission]，就把它从本名单删除；
    ///   2) **新写的控制器不要加进来** —— 不在名单内即默认拒绝（403），这是本机制的意义所在；
    ///   3) 名单清空后，本机制即为完全的「写操作默认拒绝」。
    /// </summary>
    private static readonly HashSet<string> LegacyUnpermissionedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        // TODO 待补权限（建议下一批处理，按风险从高到低）
        "SysRole",              // P0：可自我提权
        "SysUser",              // P0：可重置超管密码（Create 已补，其余待补）
        "SysConfig",
        "TaskJob",
        "Customer",             // 可重置任意会员密码
        "ProdInfo",
        "ProdSku",
        "ProdSpecItem",
        "ProdSpecValue",
        "SupSupplier",
        "Banner",
        "CmsAnnouncement",
        "DictType",
        "DictData",
        "File",
        "AdminUpload",
        "PaymentLog",
        "AfterSaleLog"
    };

    /// <summary>接口是否显式声明「有意不做角色权限校验」</summary>
    private static bool IsPermissionFree(ControllerActionDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return false;
        }

        return descriptor.MethodInfo.GetCustomAttribute<PermissionFreeAttribute>(inherit: false) is not null
               || descriptor.ControllerTypeInfo.GetCustomAttribute<PermissionFreeAttribute>(inherit: true) is not null;
    }

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
