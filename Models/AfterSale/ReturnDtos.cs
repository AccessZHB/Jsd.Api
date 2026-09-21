using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.AfterSale;

#region 入参 DTO

/// <summary>退货明细入参</summary>
public class ReturnItemInputDto
{
    /// <summary>关联订单明细ID（trx_order_item.id，可空）</summary>
    public long? OrderItemId { get; set; }

    /// <summary>商品ID（prod_info.id）</summary>
    [Required]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（prod_sku.id，库存回滚必需；不传则按商品首个 SKU 处理）</summary>
    public long? SkuId { get; set; }

    /// <summary>商品名称（快照）</summary>
    [Required]
    [StringLength(100)]
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>SKU名称（快照）</summary>
    [StringLength(100)]
    public string? SkuName { get; set; }

    /// <summary>申请退货数量（&gt; 0）</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "退货数量必须大于 0")]
    public int Quantity { get; set; }

    /// <summary>商品单价（元，快照，≥ 0）</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "商品单价不能为负")]
    public decimal UnitPrice { get; set; }
}

/// <summary>创建退货单入参（POST /api/after-sale/return）</summary>
public class CreateReturnDto
{
    /// <summary>订单ID</summary>
    [Required]
    public long OrderId { get; set; }

    /// <summary>关联退款单ID（可空；由退款审核自动生成时会传入）</summary>
    public long? RefundId { get; set; }

    /// <summary>申请人（mem_member.id）</summary>
    [Required]
    public long MemberId { get; set; }

    /// <summary>退货原因</summary>
    [Required]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "请填写退货原因（2~255 字）")]
    public string ReturnReason { get; set; } = string.Empty;

    /// <summary>退货物流公司</summary>
    [StringLength(50)]
    public string? LogisticsCompany { get; set; }

    /// <summary>退货物流单号</summary>
    [StringLength(50)]
    public string? LogisticsNo { get; set; }

    /// <summary>退货收货地址（快照）</summary>
    public string? ReceiveAddress { get; set; }

    /// <summary>客服/仓库备注</summary>
    [StringLength(255)]
    public string? AdminRemark { get; set; }

    /// <summary>退货明细（至少一条）</summary>
    [Required]
    public List<ReturnItemInputDto> Items { get; set; } = new();
}

/// <summary>仓库确认收货入参（PUT /api/after-sale/return/{id}/receive）</summary>
public class ReceiveReturnDto
{
    /// <summary>实收明细：明细ID + 实收数量（实收数量必须 ≤ 申请数量）</summary>
    [Required]
    public List<ReceiveReturnItemDto> Items { get; set; } = new();

    /// <summary>收货备注</summary>
    [StringLength(255)]
    public string? AdminRemark { get; set; }
}

/// <summary>收货明细行</summary>
public class ReceiveReturnItemDto
{
    /// <summary>退货明细ID（trx_return_item.id）</summary>
    [Required]
    public long ItemId { get; set; }

    /// <summary>实收数量（≥ 0 且 ≤ 申请退货数量；0 表示拒收）</summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "实收数量不能为负")]
    public int ReceivedQuantity { get; set; }
}

#endregion

#region 查询 DTO

/// <summary>退货单分页查询入参（GET /api/after-sale/return/list）</summary>
public class ReturnQueryDto
{
    public string? ReturnNo { get; set; }
    public string? OrderNo { get; set; }
    public string? MemberName { get; set; }
    public string? RefundNo { get; set; }
    public long? MemberId { get; set; }
    public long? RefundId { get; set; }
    /// <summary>状态：0待审核 1待退货 2已寄回 3已收货 4已退款 5已拒绝</summary>
    public int? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

#endregion

#region 出参 DTO

/// <summary>退货单列表项</summary>
public class ReturnListDto
{
    public long Id { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string? OrderNo { get; set; }
    public long? RefundId { get; set; }
    public string? RefundNo { get; set; }
    public long MemberId { get; set; }
    public string? MemberName { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
    public decimal ReturnAmount { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? LogisticsCompany { get; set; }
    public string? LogisticsNo { get; set; }
    /// <summary>明细行数（退货商品种类数）</summary>
    public int ItemCount { get; set; }
    /// <summary>申请退货总数量（Σ 明细 quantity）</summary>
    public int TotalQuantity { get; set; }
    /// <summary>实际收货总数量（Σ 明细 received_quantity）</summary>
    public int TotalReceived { get; set; }
    public DateTime? HandleTime { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>退货明细项（出参）</summary>
public class ReturnDetailItemDto
{
    public long Id { get; set; }
    public long? OrderItemId { get; set; }
    public long ProdInfoId { get; set; }
    public long? SkuId { get; set; }
    public string ProdInfoName { get; set; } = string.Empty;
    public string? SkuName { get; set; }
    /// <summary>申请退货数量</summary>
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>退货单详情（含明细）</summary>
public class ReturnDetailDto : ReturnListDto
{
    public string? ReceiveAddress { get; set; }
    public string? AdminRemark { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<ReturnDetailItemDto> Items { get; set; } = new();
}

/// <summary>创建退货单结果</summary>
public class ReturnCreateResult
{
    public long Id { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
}

#endregion
