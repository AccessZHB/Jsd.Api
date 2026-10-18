using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Customer;

/// <summary>
/// 小程序会员本人重置登录密码 请求 DTO（POST /api/customer/reset-password）
/// 说明：
///   1. 仅小程序会员可调用（CustomerController 已 [Authorize]，且方法内要求 user_type=member 声明）。
///   2. 当前会员ID 由后端从 JWT 的 member_id 声明解析，前端无需（也不应）传入，杜绝越权改他人密码。
///   3. 后端流程：校验旧密码(BCrypt 比对) → 校验新密码格式(≥6位) → 新旧不一致 → BCrypt(cost=12) 加密覆盖。
///   4. 绝不持久化明文密码。
/// </summary>
public class SelfResetPasswordDto
{
    /// <summary>原登录密码（明文，用于身份校验）</summary>
    [Required(ErrorMessage = "原密码不能为空")]
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>新登录密码（明文，6~128 位）</summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [MinLength(6, ErrorMessage = "新密码长度不能少于 6 位")]
    [StringLength(128, ErrorMessage = "新密码过长")]
    public string NewPassword { get; set; } = string.Empty;
}
