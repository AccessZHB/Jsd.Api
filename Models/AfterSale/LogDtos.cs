namespace Jsd.Api.Models.AfterSale;

/// <summary>
/// 售后日志出参（trx_refund_log 投影）
/// 所有售后写操作（提交/审核/驳回/生成退货单/收货/退款）都会写入日志，
/// 形成退款单/退货单的全链路审计轨迹。
/// </summary>
public class RefundLogDto
{
    public long Id { get; set; }
    public long RefundId { get; set; }
    /// <summary>操作动作：created/approved/rejected/return_created/return_received/refunded</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>动作中文名（RefundLogActionHelper 映射）</summary>
    public string ActionText { get; set; } = string.Empty;
    public long? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
}
