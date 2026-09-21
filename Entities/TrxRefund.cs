using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 退款申请表 trx_refund（售后模块资金流主表）
///
/// 【职责】记录一笔退款/退货退款的申请、审核与执行全过程：
///   提交申请 → 客服审核(通过/驳回) → 财务执行打款。
/// 实物退货由 trx_return 承载，二者通过 trx_return.refund_id 关联。
///
/// 【状态 status】
///   0-待审核 Pending ──审核通过──&gt; 1-审核通过/待退款 Approved
///   0 ──驳回──&gt; 3-已驳回 Rejected
///   1 ──执行退款──&gt; 2-已退款 Refunded（终态）
///
/// 【退款类型 refund_type】1-仅退款（不退实物） 2-退货退款（需先退货再退款）
///
/// 【幂等】执行退款接口通过 status 判重：仅 1-待退款 可执行，2-已退款 直接返回，杜绝重复打款。
/// </summary>
[Table("trx_refund")]
public class TrxRefund
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>退款单号（唯一，TK + 日期 + 序号，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("refund_no")]
    public string RefundNo { get; set; } = string.Empty;

    /// <summary>订单ID（关联 trx_order.id）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>订单号（冗余，便于列表检索与展示）</summary>
    [StringLength(50)]
    [Column("order_no")]
    public string? OrderNo { get; set; }

    /// <summary>申请人（关联 mem_member.id）</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    /// <summary>退款金额（元，≤ 订单实付金额 - 已退金额）</summary>
    [Column("refund_amount", TypeName = "decimal(12,2)")]
    public decimal RefundAmount { get; set; }

    /// <summary>退款类型：1-仅退款 2-退货退款</summary>
    [Column("refund_type")]
    public int RefundType { get; set; }

    /// <summary>退款原因</summary>
    [StringLength(255)]
    [Column("refund_reason")]
    public string? RefundReason { get; set; }

    /// <summary>凭证图片（多个用逗号分隔的相对路径）</summary>
    [StringLength(500)]
    [Column("voucher_images")]
    public string? VoucherImages { get; set; }

    /// <summary>状态：0-待审核 1-审核通过/待退款 2-已退款 3-已驳回</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>客服/财务处理备注</summary>
    [StringLength(255)]
    [Column("admin_remark")]
    public string? AdminRemark { get; set; }

    /// <summary>处理人ID（审核/执行操作的后台用户）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>审核处理时间</summary>
    [Column("handle_time")]
    public DateTime? HandleTime { get; set; }

    /// <summary>实际打款时间（执行退款时写入）</summary>
    [Column("refund_time")]
    public DateTime? RefundTime { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>关联订单（查询时 Include）</summary>
    public TrxOrder? Order { get; set; }
}
