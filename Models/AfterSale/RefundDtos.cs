using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.AfterSale;

#region 入参 DTO

/// <summary>提交退款申请入参（POST /api/after-sale/refund）</summary>
public class CreateRefundDto
{
    /// <summary>订单ID（trx_order.id）</summary>
    [Required]
    public long OrderId { get; set; }

    /// <summary>退款金额（元，&gt; 0 且 ≤ 订单可退金额）</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "退款金额必须大于 0")]
    public decimal RefundAmount { get; set; }

    /// <summary>退款类型：1-仅退款 2-退货退款</summary>
    [Required]
    [Range(1, 2, ErrorMessage = "退款类型不合法（1-仅退款 2-退货退款）")]
    public int RefundType { get; set; }

    /// <summary>退款原因</summary>
    [Required]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "请填写退款原因（2~255 字）")]
    public string RefundReason { get; set; } = string.Empty;

    /// <summary>凭证图片（相对路径，多个用逗号分隔）</summary>
    [StringLength(500)]
    public string? VoucherImages { get; set; }
}

/// <summary>审核退款入参（PUT /api/after-sale/refund/{id}/approve）</summary>
public class ApproveRefundDto
{
    /// <summary>是否通过：true-通过 false-驳回</summary>
    [Required]
    public bool Approved { get; set; }

    /// <summary>处理备注（驳回时建议必填）</summary>
    [StringLength(255)]
    public string? AdminRemark { get; set; }
}

#endregion

#region 查询 DTO

/// <summary>退款单分页查询入参（GET /api/after-sale/refund/list）</summary>
public class RefundQueryDto
{
    /// <summary>订单号（模糊）</summary>
    public string? OrderNo { get; set; }

    /// <summary>退款单号（模糊）</summary>
    public string? RefundNo { get; set; }

    /// <summary>客户名称（模糊，联查 mem_member.name）</summary>
    public string? MemberName { get; set; }

    /// <summary>退款状态：0待审核 1待退款 2已退款 3已驳回</summary>
    public int? Status { get; set; }

    /// <summary>申请时间起</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>申请时间止</summary>
    public DateTime? EndTime { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

#endregion

#region 出参 DTO

/// <summary>退款单列表项</summary>
public class RefundListDto
{
    public long Id { get; set; }
    public string RefundNo { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string? OrderNo { get; set; }
    public long MemberId { get; set; }
    /// <summary>客户名称（联查 mem_member 回填）</summary>
    public string? MemberName { get; set; }
    public decimal RefundAmount { get; set; }
    public int RefundType { get; set; }
    public string RefundTypeText { get; set; } = string.Empty;
    public string? RefundReason { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    /// <summary>订单实付金额（便于核对可退额度）</summary>
    public decimal OrderPayAmount { get; set; }
    public DateTime? HandleTime { get; set; }
    public DateTime? RefundTime { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>退款单详情</summary>
public class RefundDetailDto : RefundListDto
{
    /// <summary>凭证图片（多个用逗号分隔）</summary>
    public string? VoucherImages { get; set; }
    public string? AdminRemark { get; set; }
    public long? OperatorId { get; set; }
    public DateTime UpdateTime { get; set; }
    /// <summary>关联的退货单（退货退款时存在）</summary>
    public List<RefundRelatedReturnDto> Returns { get; set; } = new();
    /// <summary>全链路操作日志</summary>
    public List<RefundLogDto> Logs { get; set; } = new();
}

/// <summary>详情中附带的关联退货单摘要</summary>
public class RefundRelatedReturnDto
{
    public long Id { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public decimal ReturnAmount { get; set; }
}

/// <summary>创建退款申请结果</summary>
public class RefundCreateResult
{
    public long Id { get; set; }
    public string RefundNo { get; set; } = string.Empty;
}

#endregion
