using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 小程序会员（买家）自助接口：登录 + 当前会员资料。
///
/// 与后台管理员体系（api/auth、sys_user）完全隔离：
///   管理员用 AuthController 登录，令牌带 is_super / role_id；
///   会员用本控制器登录，令牌带 user_type=member / member_id。
/// 两套账号体系物理分表（sys_user vs mem_member），令牌互不通用。
/// </summary>
[ApiController]
[Route("api/member")]
public class MemberAuthController : ControllerBase
{
    private readonly IMemMemberService _memberService;

    public MemberAuthController(IMemMemberService memberService)
    {
        _memberService = memberService;
    }

    /// <summary>
    /// 小程序会员登录
    /// 请求体：{ "userName":"shengdai01", "password":"123456" }
    /// 成功返回：token / expiresIn / memberId / memberNo / name / levelName / balance
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]   // 登录接口不需要 Token
    public async Task<ApiResponse<MemberLoginResultDto>> Login([FromBody] MemberLoginDto dto)
    {
        return await _memberService.LoginAsync(dto);
    }

    /// <summary>
    /// 当前登录会员资料（小程序「我的」页）
    /// 请求头：Authorization: Bearer {会员登录返回的 token}
    /// 会员ID 从 JWT 的 member_id 声明读取；管理员令牌无此声明，返回 401。
    /// 示例：GET /api/member/profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ApiResponse<MemberProfileDto>> Profile()
    {
        // 会员令牌独有 member_id 声明；管理员令牌没有 → 视为非会员
        var raw = User.FindFirst("member_id")?.Value;
        if (!long.TryParse(raw, out var memberId) || memberId <= 0)
        {
            return ApiResponse<MemberProfileDto>.Fail("未登录或不是会员令牌", 401);
        }

        return await _memberService.GetProfileAsync(memberId);
    }
}
