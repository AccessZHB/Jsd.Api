using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Customer;

/// <summary>
/// 会员等级（对应 mem_member.level）
/// </summary>
public enum CustomerLevel
{
    /// <summary>0-普通会员</summary>
    Normal = 0,
    /// <summary>1-VIP会员</summary>
    Vip = 1,
    /// <summary>2-核心会员</summary>
    Core = 2
}

/// <summary>
/// 会员状态（对应 mem_member.status）
/// </summary>
public enum CustomerStatus
{
    /// <summary>0-禁用</summary>
    Disabled = 0,
    /// <summary>1-启用</summary>
    Enabled = 1
}

/// <summary>
/// 客户管理 权限标识（与前端 v-permission 指令、sys_menu.permission 保持一致）
/// </summary>
public static class CustomerPermissions
{
    public const string List = "customer:list";
    public const string Query = "customer:query";
    public const string Add = "customer:add";
    public const string Edit = "customer:edit";
    public const string Del = "customer:del";
}

/// <summary>
/// 会员等级 / 状态 名称映射（与前端 types/customer.ts 的 CUSTOMER_LEVEL_MAP / CUSTOMER_STATUS_MAP 保持一致）
/// </summary>
public static class CustomerMaps
{
    /// <summary>等级 → 中文名</summary>
    public static string GetLevelName(int level) => level switch
    {
        (int)CustomerLevel.Vip => "VIP会员",
        (int)CustomerLevel.Core => "核心会员",
        _ => "普通会员"
    };

    /// <summary>状态 → 中文名</summary>
    public static string GetStatusName(int status) => status == (int)CustomerStatus.Enabled ? "启用" : "禁用";

    /// <summary>订单状态 → 中文名（订单历史列表展示用，对齐订单模块 OrderStatuses）</summary>
    public static string GetOrderStatusName(int orderStatus) => orderStatus switch
    {
        0 => "待付款",
        1 => "待发货",
        2 => "待收货",
        3 => "已完成",
        4 => "已关闭",
        _ => "未知"
    };
}

/// <summary>
/// 客户分页查询参数（GET /api/customer/page）
/// </summary>
public class CustomerPageQuery
{
    /// <summary>客户名称 / 公司名称 / 会员编号 关键词（模糊，可空）</summary>
    public string? Keyword { get; set; }

    /// <summary>会员等级筛选：0-普通 1-VIP 2-核心（可空=全部）</summary>
    public int? Level { get; set; }

    /// <summary>状态筛选：1-启用 0-禁用（可空=全部）</summary>
    public int? Status { get; set; }

    /// <summary>注册时间起 yyyy-MM-dd（可空）</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>注册时间止 yyyy-MM-dd（可空）</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>页码，默认1</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数，默认10</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// 新增 / 编辑 客户请求 DTO（POST /api/customer、PUT /api/customer/{id}）
/// 说明：新增时密码必填；编辑时密码可选（留空表示不修改）。
/// </summary>
public class CustomerSaveDto
{
    /// <summary>主键ID（新增时传 0 或忽略，编辑时必传）</summary>
    public long Id { get; set; }

    /// <summary>客户名称 / 联系人姓名（必填）</summary>
    [Required(ErrorMessage = "客户名称不能为空")]
    [StringLength(100, ErrorMessage = "客户名称最长 100 个字符")]
    public string Name { get; set; } = string.Empty;

    /// <summary>公司名称（可空）</summary>
    [StringLength(200, ErrorMessage = "公司名称最长 200 个字符")]
    public string? CompanyName { get; set; }

    /// <summary>会员等级：0-普通 1-VIP 2-核心（默认 0）</summary>
    public int Level { get; set; } = 0;

    /// <summary>折扣率（0.00~1.00，默认 1.00 不打折）</summary>
    [Range(0.00, 1.00, ErrorMessage = "折扣率必须在 0~1 之间")]
    public decimal DiscountRate { get; set; } = 1.00m;

    /// <summary>信用额度（≥0，单位：元）</summary>
    [Range(0, double.MaxValue, ErrorMessage = "信用额度不能为负")]
    public decimal CreditLimit { get; set; } = 0.00m;

    /// <summary>账期天数（≥0）</summary>
    [Range(0, int.MaxValue, ErrorMessage = "账期不能为负")]
    public int AccountPeriod { get; set; } = 0;

    /// <summary>登录账号（唯一，必填）</summary>
    [Required(ErrorMessage = "登录账号不能为空")]
    [StringLength(50, ErrorMessage = "登录账号最长 50 个字符")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码（新增必填，编辑可选）。
    /// 前端传输明文，后端用 BCrypt 加密后存储，绝不持久化明文。
    /// </summary>
    [StringLength(128, ErrorMessage = "密码过长")]
    public string? Password { get; set; }

    /// <summary>联系电话（可空）</summary>
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>电子邮箱（可空，简单格式校验）</summary>
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; set; }

    /// <summary>业务联系人（可空）</summary>
    [StringLength(50)]
    public string? ContactPerson { get; set; }

    /// <summary>联系地址（可空）</summary>
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>备注（可空）</summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>状态：1-启用 0-禁用（默认 1）</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 客户列表项 DTO（GET /api/customer/page 单行）
/// 含：等级中文名、状态中文名、累计消费、最近订单信息（会员编号由后端回填，列表无需再查）。
/// </summary>
public class CustomerListItemDto
{
    public long Id { get; set; }

    /// <summary>会员编号</summary>
    public string MemberNo { get; set; } = string.Empty;

    /// <summary>客户名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>公司名称</summary>
    public string? CompanyName { get; set; }

    /// <summary>会员等级数值</summary>
    public int Level { get; set; }

    /// <summary>会员等级中文名（后端回填）</summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>折扣率</summary>
    public decimal DiscountRate { get; set; }

    /// <summary>信用额度</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>账期（天）</summary>
    public int AccountPeriod { get; set; }

    /// <summary>累计消费（元）</summary>
    public decimal TotalConsumption { get; set; }

    /// <summary>累计订单数</summary>
    public int OrderCount { get; set; }

    /// <summary>已完成订单数</summary>
    public int CompletedOrderCount { get; set; }

    /// <summary>最近订单号（支付时回写；可空）</summary>
    public string? LastOrderNo { get; set; }

    /// <summary>最近下单时间（可空）</summary>
    public DateTime? LastOrderTime { get; set; }

    /// <summary>登录账号</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>状态数值</summary>
    public int Status { get; set; }

    /// <summary>状态中文名</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>禁用标记（前端可据此做置灰/提示；1=禁用）</summary>
    public bool Disabled => Status == (int)CustomerStatus.Disabled;
}

/// <summary>
/// 客户详情 DTO（GET /api/customer/{id}）= 列表项 + 更多联系/备注信息
/// </summary>
public class CustomerDetailDto : CustomerListItemDto
{
    /// <summary>电子邮箱</summary>
    public string? Email { get; set; }

    /// <summary>业务联系人</summary>
    public string? ContactPerson { get; set; }

    /// <summary>联系地址</summary>
    public string? Address { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 客户订单历史项 DTO（GET /api/customer/{id}/orders 单行）
/// 仅返回订单概览字段，不含明细（明细走订单模块详情接口）。
/// </summary>
public class CustomerOrderItemDto
{
    public long Id { get; set; }

    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>实付金额</summary>
    public decimal PayAmount { get; set; }

    /// <summary>订单状态数值</summary>
    public int OrderStatus { get; set; }

    /// <summary>订单状态中文名</summary>
    public string OrderStatusName { get; set; } = string.Empty;

    /// <summary>下单时间</summary>
    public DateTime CreateTime { get; set; }
}
