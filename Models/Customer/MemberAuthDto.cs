using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Customer;

/// <summary>
/// 小程序会员登录请求（mem_member.username + 明文密码）
/// </summary>
public class MemberLoginDto
{
    /// <summary>登录账号（mem_member.username，全局唯一）</summary>
    [Required(ErrorMessage = "登录账号不能为空")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码（明文传输，服务端用 BCrypt 校验；生产环境建议 HTTPS）</summary>
    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 小程序会员登录成功结果
/// </summary>
public class MemberLoginResultDto
{
    /// <summary>JWT 访问令牌（前端后续请求放 Header：Authorization: Bearer {token}）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（秒）</summary>
    public int ExpiresIn { get; set; }

    /// <summary>会员ID（mem_member.id）</summary>
    public long MemberId { get; set; }

    /// <summary>会员编号</summary>
    public string MemberNo { get; set; } = string.Empty;

    /// <summary>客户名称 / 联系人</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>昵称（默认取 Name，小程序可直接展示）</summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>等级中文名（如：普通会员 / VIP会员 / 核心会员）</summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>可用余额（元）</summary>
    public decimal Balance { get; set; }
}

/// <summary>
/// 当前登录会员资料（小程序「我的」页用）
/// </summary>
public class MemberProfileDto
{
    public long MemberId { get; set; }
    public string MemberNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>等级：0-普通会员 1-VIP会员 2-核心会员</summary>
    public int Level { get; set; }

    /// <summary>等级中文名</summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>可用余额（元）</summary>
    public decimal Balance { get; set; }

    /// <summary>折扣率（0.00~1.00）</summary>
    public decimal DiscountRate { get; set; }

    /// <summary>信用额度（元）</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>状态：1-启用 0-禁用</summary>
    public int Status { get; set; }
}
