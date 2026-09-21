using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Purchase;

#region 入参 DTO

/// <summary>付款核销入参</summary>
public class PayPaymentDto
{
    /// <summary>付款金额（&gt; 0 且 ≤ 未付金额）</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "付款金额必须大于 0")]
    public decimal PayAmount { get; set; }

    /// <summary>付款方式（1-银行转账 2-支票 3-现金 4-其他）</summary>
    [Required]
    [Range(1, 4, ErrorMessage = "付款方式不合法")]
    public int PayMethod { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }
}

#endregion

#region 查询 DTO

/// <summary>应付账款分页查询入参</summary>
public class PayableQueryDto
{
    public long? SupplierId { get; set; }
    public int? Status { get; set; }
    public DateTime? DueDateStart { get; set; }
    public DateTime? DueDateEnd { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

#endregion

#region 出参 DTO

/// <summary>应付账款列表项</summary>
public class PayableListDto
{
    public long Id { get; set; }
    public string PayableNo { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long? RelatedOrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>应付账款详情</summary>
public class PayableDetailDto
{
    public long Id { get; set; }
    public string PayableNo { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long? RelatedOrderId { get; set; }
    public string? RelatedOrderNo { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>付款/生成结果</summary>
public class PayableCreateResult
{
    public long Id { get; set; }
    public string PayableNo { get; set; } = string.Empty;
}

#endregion
