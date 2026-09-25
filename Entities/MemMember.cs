using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 会员 / 客户基础信息表 mem_member（依据需求逐列设计，对应数据库表 mem_member）
///
/// 业务定位：
///   本表是 B2B 订货系统中的"买家/客户"主数据。订单表 trx_order.buyer_id 即指向本表 id。
///   累计消费、累计订单数、最近订单等统计字段，由订单支付/完成时由 OrderService 自动回写（联动）。
///
/// 与后台管理员 sys_user 物理隔离：sys_user 是内部运营账号，mem_member 是外部买家，
/// 两者绝不混用（登录、权限体系分开）。
/// </summary>
[Table("mem_member")]
public class MemMember
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>会员编号（唯一，自动生成，格式：M + yyyyMMddHHmmss + 4位随机数）</summary>
    [Required]
    [StringLength(32)]
    [Column("member_no")]
    public string MemberNo { get; set; } = string.Empty;

    /// <summary>客户名称 / 联系人姓名（必填）</summary>
    [Required]
    [StringLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>公司名称（B2B 场景一般为企业客户，可空）</summary>
    [StringLength(200)]
    [Column("company_name")]
    public string? CompanyName { get; set; }

    /// <summary>
    /// 会员等级：0-普通会员 1-VIP会员 2-核心会员
    /// 名称映射见 Models/Customer/CustomerDto 的 GetLevelName()。
    /// </summary>
    [Column("level")]
    public int Level { get; set; } = 0;

    /// <summary>折扣率（0.00~1.00，1.00 表示不打折；下单时按此折扣计算）</summary>
    [Column("discount_rate")]
    public decimal DiscountRate { get; set; } = 1.00m;

    /// <summary>信用额度（允许赊账上限，单位：元；0 表示不支持赊账）</summary>
    [Column("credit_limit")]
    public decimal CreditLimit { get; set; } = 0.00m;

    /// <summary>账期（允许赊账的天数；0 表示款到发货）</summary>
    [Column("account_period")]
    public int AccountPeriod { get; set; } = 0;

    // ===================== 价格策略与会员余额模块（扩展） =====================

    /// <summary>
    /// 客户等级ID（关联 mem_member_level.id，价格策略与会员余额模块扩展）。
    /// 取值引擎据此匹配「客户等级价」规则；为 0/NULL 表示未分级，只能走客户专属价或标准售价。
    /// </summary>
    [Column("customer_level_id")]
    public long? CustomerLevelId { get; set; }

    /// <summary>
    /// 可用余额（元，decimal(10,2)）。
    /// 【先充值后下单】下单前必须校验 balance ≥ 订单应付金额，不足则拦截下单。
    /// 所有变动必须与 mkt_balance_log 严格勾稽（同事务写入）。
    /// </summary>
    [Column("balance", TypeName = "decimal(10,2)")]
    public decimal Balance { get; set; } = 0.00m;

    /// <summary>
    /// 冻结余额（元，decimal(10,2)）。
    /// 下单时从 balance 等额转入本字段（资金"锁定"），支付成功时从本字段扣减；
    /// 取消/退款时从本字段退回 balance。保证不会出现"余额被两笔订单同时占用"。
    /// </summary>
    [Column("frozen_balance", TypeName = "decimal(10,2)")]
    public decimal FrozenBalance { get; set; } = 0.00m;

    /// <summary>
    /// 累计充值总额（元，decimal(10,2)）。
    /// 只累加 mem_recharge.recharge_amount 本金（赠送金额 gift_amount 不计入），用于客户价值分析。
    /// </summary>
    [Column("total_recharge", TypeName = "decimal(10,2)")]
    public decimal TotalRecharge { get; set; } = 0.00m;

    // ===================== 以下为"累计统计"字段，由订单联动回写 =====================

    /// <summary>累计消费金额（已支付订单的实付金额之和，单位：元）</summary>
    [Column("total_consumption")]
    public decimal TotalConsumption { get; set; } = 0.00m;

    /// <summary>累计订单数（已支付订单笔数）</summary>
    [Column("order_count")]
    public int OrderCount { get; set; } = 0;

    /// <summary>已完成订单数（已确认收货/完成的订单笔数）</summary>
    [Column("completed_order_count")]
    public int CompletedOrderCount { get; set; } = 0;

    /// <summary>最近一笔订单的订单号（支付时回写，便于列表"最近订单"展示；可空）</summary>
    [Column("last_order_no")]
    public string? LastOrderNo { get; set; }

    /// <summary>最近下单时间（支付时回写；可空）</summary>
    [Column("last_order_time")]
    public DateTime? LastOrderTime { get; set; }

    // ===================== 登录账号（BCrypt 加密） =====================

    /// <summary>登录账号（唯一，用于客户小程序/前台登录）</summary>
    [Required]
    [StringLength(50)]
    [Column("username")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码（BCrypt 加密后的密文，不存明文）</summary>
    [Required]
    [StringLength(128)]
    [Column("password")]
    public string Password { get; set; } = string.Empty;

    // ===================== 联系方式 / 备注 =====================

    /// <summary>联系电话</summary>
    [StringLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    /// <summary>电子邮箱</summary>
    [StringLength(100)]
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>业务联系人（可不同于 Name，一般用于企业客户对接人）</summary>
    [StringLength(50)]
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    /// <summary>联系地址</summary>
    [StringLength(500)]
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>状态：1-启用 0-禁用（禁用后不可登录）</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>逻辑删除：0-正常 1-已删除（软删除，列表查询统一过滤）</summary>
    [Column("is_deleted")]
    public int IsDeleted { get; set; } = 0;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
