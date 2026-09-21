using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Purchase;

#region 入参 DTO

/// <summary>采购订单明细入参（创建/更新共用）</summary>
public class PurchaseOrderItemInputDto
{
    /// <summary>物料/SKU ID（关联 prod_sku.id）</summary>
    [Required]
    public long MaterialId { get; set; }

    /// <summary>采购数量（&gt; 0）</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "采购数量必须大于 0")]
    public decimal Quantity { get; set; }

    /// <summary>采购单价（元，≥ 0）</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "采购单价不能为负")]
    public decimal UnitPrice { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
    public string? Remark { get; set; }
}

/// <summary>创建采购订单入参</summary>
public class CreatePurchaseOrderDto
{
    /// <summary>供应商ID</summary>
    [Required]
    public long SupplierId { get; set; }

    /// <summary>下单日期（yyyy-MM-dd）</summary>
    [Required]
    public DateTime OrderDate { get; set; }

    /// <summary>预计到货日期</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>采购明细</summary>
    [Required]
    public List<PurchaseOrderItemInputDto> Items { get; set; } = new();
}

/// <summary>更新采购订单入参（仅草稿可编辑，结构同创建）</summary>
public class UpdatePurchaseOrderDto
{
    [Required]
    public long SupplierId { get; set; }

    [Required]
    public DateTime OrderDate { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    [StringLength(500)]
    public string? Remark { get; set; }

    [Required]
    public List<PurchaseOrderItemInputDto> Items { get; set; } = new();
}

/// <summary>库存预警自动生成采购订单入参</summary>
public class AutoCreateOrderDto
{
    /// <summary>库存预警ID列表；为空则扫描所有启用且触发预警的配置</summary>
    public List<long>? WarningIds { get; set; }
}

#endregion

#region 查询 DTO

/// <summary>采购订单分页查询入参</summary>
public class PurchaseOrderQueryDto
{
    /// <summary>供应商ID</summary>
    public long? SupplierId { get; set; }

    /// <summary>订单状态</summary>
    public int? Status { get; set; }

    /// <summary>下单日期起</summary>
    public DateTime? OrderDateStart { get; set; }

    /// <summary>下单日期止</summary>
    public DateTime? OrderDateEnd { get; set; }

    /// <summary>关键词（单号/供应商名称模糊匹配）</summary>
    public string? Keyword { get; set; }

    /// <summary>页码，默认 1</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数，默认 20</summary>
    public int PageSize { get; set; } = 20;
}

#endregion

#region 出参 DTO

/// <summary>采购订单列表项</summary>
public class PurchaseOrderListDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>采购订单明细项（出参）</summary>
public class PurchaseOrderDetailItemDto
{
    public long Id { get; set; }
    public long MaterialId { get; set; }
    /// <summary>物料名称（SKU 名称，Service 回填）</summary>
    public string? MaterialName { get; set; }
    /// <summary>所属商品ID（prod_info.id，Service 回填；前端编辑时用它在"商品→SKU"级联里回显）</summary>
    public long? ProdInfoId { get; set; }
    /// <summary>规格展示文本（prod_sku.spec_values，Service 回填）</summary>
    public string? SpecValues { get; set; }
    /// <summary>计量单位（prod_info.unit，Service 回填）</summary>
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string? Remark { get; set; }
}

/// <summary>采购订单详情（含明细与入库记录）</summary>
public class PurchaseOrderDetailDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<PurchaseOrderDetailItemDto> Items { get; set; } = new();
    /// <summary>已入库数量合计（所有明细已收之和）</summary>
    public decimal ReceivedTotal { get; set; }
    /// <summary>未入库数量合计</summary>
    public decimal UnreceivedTotal { get; set; }
}

/// <summary>创建/自动生成结果</summary>
public class PurchaseOrderCreateResult
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
}

#endregion
