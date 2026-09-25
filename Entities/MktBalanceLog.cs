using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 余额流水表 mkt_balance_log（余额真值台账）
///
/// 【铁律】本表是余额对账的【唯一真值来源】：
///   任何一次余额变动（充值 / 下单扣减 / 退款退回 / 后台调整 / 冻结 / 解冻）都必须落一行，
///   且 before_balance / after_balance 必须与 mem_member 表实时余额【严格勾稽】：
///       before_balance + change_amount = after_balance
///       after_balance = 本次操作后 mem_member.balance（冻结/解冻则为 frozen_balance 侧）
///
/// 【变动类型 change_type】1-充值 2-下单扣减 3-退款退回 4-后台调整 5-冻结 6-解冻
///   其中 5/6 作用于 frozen_balance（可用余额与冻结额互为转移），1/2/3/4 作用于可用余额。
///   约定：change_amount 一律记【正数】，方向由 change_type 语义决定（扣减/冻结也存正数），
///         便于对账时直接 Σ 而不必判断符号。
/// </summary>
[Table("mkt_balance_log")]
public class MktBalanceLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>会员ID（mem_member.id）</summary>
    [Column("mem_member_id")]
    public long MemMemberId { get; set; }

    /// <summary>变动类型：1-充值 2-下单扣减 3-退款退回 4-后台调整 5-冻结 6-解冻</summary>
    [Column("change_type")]
    public int ChangeType { get; set; }

    /// <summary>变动金额（正数，方向由 change_type 决定）</summary>
    [Column("change_amount", TypeName = "decimal(10,2)")]
    public decimal ChangeAmount { get; set; }

    /// <summary>变动前余额</summary>
    [Column("before_balance", TypeName = "decimal(10,2)")]
    public decimal BeforeBalance { get; set; }

    /// <summary>变动后余额</summary>
    [Column("after_balance", TypeName = "decimal(10,2)")]
    public decimal AfterBalance { get; set; }

    /// <summary>
    /// 关联单据类型：0-无 1-订单 2-充值订单 3-退款单
    /// ⚠️ 数据库该列是 TINYINT（见 Jsd_order.sql），不是需求文档里的 varchar，
    ///    故改用数值枚举 BalanceRelatedType，落库存数字。
    /// </summary>
    [Column("related_type")]
    public int? RelatedType { get; set; }

    /// <summary>关联业务ID（充值单ID / 订单ID / 退款单ID）</summary>
    [Column("related_id")]
    public long? RelatedId { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>操作人ID（后台调整/解冻时必填，客户自助操作可为 NULL）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
