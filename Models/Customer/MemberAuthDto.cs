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

    /// <summary>等级中文名（如：金牌客户 / VIP客户；未分级为"未分级"）</summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>客户等级ID（关联 mem_member_level.id，null=未分级；小程序可据此请求会员价）</summary>
    public long? CustomerLevelId { get; set; }

    /// <summary>
    /// 折扣率（0.00~1.00，由等级的 default_discount 动态带出）。
    /// 小程序仅用于展示（如"会员价"标签），**实际成交价一律以后端取价结果为准**，前端不参与计算。
    /// </summary>
    public decimal DiscountRate { get; set; } = 1.00m;

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

    /// <summary>
    /// 客户等级ID（关联 mem_member_level.id；null/0 = 未分级）。
    /// ⚠️ 旧的 Level(0/1/2 枚举) 已随 mem_member.level 列一并废弃删除。
    /// </summary>
    public long? CustomerLevelId { get; set; }

    /// <summary>等级中文名（由【客户等级】表动态带出；未分级为"未分级"）</summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>可用余额（元）</summary>
    public decimal Balance { get; set; }

    /// <summary>折扣率（0.00~1.00，由等级的 default_discount 动态带出，客户表不再落库存储）</summary>
    public decimal DiscountRate { get; set; } = 1.00m;

    /// <summary>信用额度（元）</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>状态：1-启用 0-禁用</summary>
    public int Status { get; set; }
}
