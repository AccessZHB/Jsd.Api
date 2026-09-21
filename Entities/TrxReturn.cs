using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 退货单表 trx_return（售后模块物流流主表）
///
/// 【职责】记录客户退货的实物退回：退货原因、物流信息、仓库收货确认。
/// 与 trx_refund 配合：trx_refund 管"钱"，trx_return 管"货"，通过 refund_id 联动。
///
/// 【状态 status】
///   0-待审核 ──审核──&gt; 1-已审核/待退货 ──客户寄回──&gt; 2-已发货/已寄回
///   2 ──仓库收货──&gt; 3-仓库已收货 ──(退货退款)自动触发退款──&gt; 4-已退款
///   任意前置状态 ──拒绝──&gt; 5-已拒绝
///
/// 【自动生成】审核"退货退款"类型的退款单时，系统自动生成一张 1-已审核/待退货 的退货单。
/// </summary>
[Table("trx_return")]
public class TrxReturn
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>退货单号（唯一，TH + 日期 + 序号，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("return_no")]
    public string ReturnNo { get; set; } = string.Empty;

    /// <summary>订单ID（关联 trx_order.id）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>关联退款单ID（trx_refund.id，仅退款时为空）</summary>
    [Column("refund_id")]
    public long? RefundId { get; set; }

    /// <summary>申请人（关联 mem_member.id）</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    /// <summary>退货原因（如度数做错/质量问题/七天无理由）</summary>
    [Required]
    [StringLength(255)]
    [Column("return_reason")]
    public string ReturnReason { get; set; } = string.Empty;

    /// <summary>退货涉及金额（元）</summary>
    [Column("return_amount", TypeName = "decimal(12,2)")]
    public decimal ReturnAmount { get; set; }

    /// <summary>状态：0-待审核 1-已审核/待退货 2-已发货/已寄回 3-仓库已收货 4-已退款 5-已拒绝</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>退货物流公司</summary>
    [StringLength(50)]
    [Column("logistics_company")]
    public string? LogisticsCompany { get; set; }

    /// <summary>退货物流单号</summary>
    [StringLength(50)]
    [Column("logistics_no")]
    public string? LogisticsNo { get; set; }

    /// <summary>退货收货地址（快照）</summary>
    [Column("receive_address")]
    public string? ReceiveAddress { get; set; }

    /// <summary>客服/仓库备注</summary>
    [StringLength(255)]
    [Column("admin_remark")]
    public string? AdminRemark { get; set; }

    /// <summary>处理时间（审核/收货等关键动作时间）</summary>
    [Column("handle_time")]
    public DateTime? HandleTime { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>退货明细集合</summary>
    public List<TrxReturnItem> Items { get; set; } = new();

    /// <summary>关联退款单（查询时 Include）</summary>
    public TrxRefund? Refund { get; set; }
}
