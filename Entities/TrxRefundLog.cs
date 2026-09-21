using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 售后日志表 trx_refund_log（售后模块操作留痕）
///
/// 每次售后状态流转都在此表插入一条记录，形成全链路审计轨迹：
///   created(提交申请) → approved(审核通过) / rejected(驳回)
///   → return_created(生成退货单) → return_received(仓库收货)
///   → refunded(执行退款)
///
/// 约定：所有售后写操作（提交/审核/驳回/生成退货单/收货/退款）必须写日志，
///       由 Service 在同一事务内落库，保证"业务与日志"要么都成功要么都回滚。
/// </summary>
[Table("trx_refund_log")]
public class TrxRefundLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>售后单ID（关联 trx_refund.id）</summary>
    [Column("refund_id")]
    public long RefundId { get; set; }

    /// <summary>操作动作：created/approved/rejected/return_created/return_received/refunded</summary>
    [Required]
    [StringLength(50)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>操作人员ID（后台管理员/客服；系统自动流程时为 null）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>操作人名称（冗余，便于查询展示）</summary>
    [StringLength(50)]
    [Column("operator_name")]
    public string? OperatorName { get; set; }

    /// <summary>备注/处理说明</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
