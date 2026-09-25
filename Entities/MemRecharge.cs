using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 充值订单表 mem_recharge（价格策略与会员余额模块）
///
/// 【业务背景】本系统执行「先充值后下单」：客户必须先把钱充进余额，才能下单。
///   充值单是资金入口的唯一凭证，入账动作只发生在支付回调（wechat-notify）。
///
/// 【状态 status】0-待支付 1-已支付（已入账） 2-已退款 3-失败
/// 【幂等】transaction_id 唯一索引是防重复入账的最后一道防线：
///   微信重复回调时，Service 先按 transaction_id 查库，已支付则直接返回成功，严禁二次入账。
/// 【金额口径】入账金额 = recharge_amount（本金） + gift_amount（赠送），
///   但 total_recharge（累计充值）只累加本金，赠送不计入累计充值。
/// </summary>
[Table("mem_recharge")]
public class MemRecharge
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>充值单号（唯一，RC + yyyyMMddHHmmss + 4位随机数，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("recharge_no")]
    public string RechargeNo { get; set; } = string.Empty;

    /// <summary>会员ID（mem_member.id）</summary>
    [Column("mem_member_id")]
    public long MemMemberId { get; set; }

    /// <summary>充值本金（元）</summary>
    [Column("recharge_amount", TypeName = "decimal(10,2)")]
    public decimal RechargeAmount { get; set; }

    /// <summary>赠送金额（元，营销活动赠送，不开发票部分）</summary>
    [Column("gift_amount", TypeName = "decimal(10,2)")]
    public decimal GiftAmount { get; set; }

    /// <summary>
    /// 支付方式：1-微信支付 2-线下转账
    /// ⚠️ 数据库列名/取值以 Jsd_order.sql 的 mem_recharge 为准（pay_type TINYINT），
    ///    需求文档里的 pay_channel 字符串改在 DTO 层对外暴露（RechargeDto.PayChannel），
    ///    这样前端 JSON 契约不变，落库用数值枚举。
    /// </summary>
    [Column("pay_type")]
    public int PayType { get; set; } = 1;

    /// <summary>
    /// 充值渠道：1-小程序自助 2-后台代充（区分是谁发起的充值，便于对账）
    /// </summary>
    [Column("recharge_channel")]
    public int RechargeChannel { get; set; } = 1;

    /// <summary>支付流水号（微信 transaction_id；唯一，幂等键）</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>
    /// 支付状态：0-待支付 1-已到账 2-已关闭 3-已退款
    /// ⚠️ 数据库列名为 pay_status，且取值语义与需求文档的 status 不同（2=已关闭而非已退款），
    ///    一律以数据库注释为准（见 Jsd_order.sql）。
    /// </summary>
    [Column("pay_status")]
    public int Status { get; set; }

    /// <summary>后台代充操作人ID（sys_user.id；自助充值为 NULL）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>备注 / 凭证说明（资金操作必须可审计）</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>支付时间（回调入账时写入）</summary>
    [Column("pay_time")]
    public DateTime? PayTime { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
