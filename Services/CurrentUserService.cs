using System.Security.Claims;

namespace Jsd.Api.Services;

/// <summary>
/// 当前登录用户服务：从 JWT 解析出的 Claims 中读取当前请求的用户信息。
/// 任何需要"当前登录人"的 Service 都可以注入它。
/// </summary>
public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>当前登录用户ID（未登录或解析失败返回 0）</summary>
    public long UserId
    {
        get
        {
            var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(id, out var uid) ? uid : 0;
        }
    }

    /// <summary>当前登录账号</summary>
    public string UserName
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    /// <summary>
    /// 当前登录会员ID（小程序会员令牌专用）。
    /// 优先读自定义声明 member_id；缺失时回退到 NameIdentifier（会员令牌两者都设了），
    /// 保证确认订单页 / 提交接口能从 JWT 安全拿到会员身份（买家 = 当前会员）。
    /// </summary>
    public long MemberId
    {
        get
        {
            var m = _httpContextAccessor.HttpContext?.User?.FindFirstValue("member_id");
            if (long.TryParse(m, out var mid) && mid > 0)
            {
                return mid;
            }
            return UserId;
        }
    }

    /// <summary>是否超级管理员（JWT 中 is_super 声明为 "1"）</summary>
    public bool IsSuper
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue("is_super") == "1";

    /// <summary>
    /// 当前令牌是否为【会员令牌】（携带 member_id 声明；管理员令牌无此声明）。
    /// 用于区分"会员自助接口"（必须按 buyer_id 强制归属过滤，绝不越权看他人订单）
    /// 与"后台管理接口"（可查看全部订单）。
    /// 判定依据与 MemberAuthController 一致：会员令牌独有 member_id 声明。
    /// </summary>
    public bool IsMember
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue("member_id") != null;

    /// <summary>当前登录用户角色ID（JWT 中 role_id 声明；未绑定角色或解析失败返回 null）</summary>
    public long? RoleId
    {
        get
        {
            var v = _httpContextAccessor.HttpContext?.User?.FindFirstValue("role_id");
            return long.TryParse(v, out var r) && r > 0 ? r : null;
        }
    }
}
