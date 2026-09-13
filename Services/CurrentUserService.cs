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

    /// <summary>是否超级管理员（JWT 中 is_super 声明为 "1"）</summary>
    public bool IsSuper
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue("is_super") == "1";
}
