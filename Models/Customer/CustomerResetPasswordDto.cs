using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Customer;

/// <summary>
/// 后台重置客户（会员）登录密码 请求 DTO（POST /api/customer/{id}/reset-password）
/// 说明：
///   1. 仅后台管理员可调用（CustomerController 已 [Authorize]，且方法内拒绝会员令牌 user_type=member）。
///   2. 传入明文新密码，后端统一用 BCrypt（cost=12）加密后覆盖原密码，绝不持久化明文。
///   3. 长度下限 6 位（与新增/编辑客户密码规则保持一致）。
/// </summary>
public class CustomerResetPasswordDto
{
    /// <summary>新登录密码（明文，6~128 位）</summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [MinLength(6, ErrorMessage = "新密码长度不能少于 6 位")]
    [StringLength(128, ErrorMessage = "新密码过长")]
    public string NewPassword { get; set; } = string.Empty;
}
