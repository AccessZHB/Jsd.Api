using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Purchase;

#region 入参 DTO

/// <summary>入库明细入参（以采定入）</summary>
public class InboundItemInputDto
{
    /// <summary>采购订单明细ID（pur_order_item.id）</summary>
    [Required]
    public long OrderItemId { get; set; }

    /// <summary>实收数量（&gt; 0，且不超过该明细剩余未入数量）</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "实收数量必须大于 0")]
    public decimal ActualQuantity { get; set; }

    /// <summary>入库批次号</summary>
    [StringLength(32)]
    public string? BatchNo { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
    public string? Remark { get; set; }
}

/// <summary>创建入库单入参</summary>
public class CreateInboundDto
{
    /// <summary>采购订单ID（须为"审批通过/待入库"状态）</summary>
    [Required]
    public long OrderId { get; set; }

    /// <summary>仓库ID</summary>
    [Required]
    public long WarehouseId { get; set; }

    /// <summary>入库日期（yyyy-MM-dd）</summary>
    [Required]
    public DateTime InboundDate { get; set; }

    /// <summary>入库操作员ID</summary>
    public long? OperatorId { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>入库明细（实收，可分批）</summary>
    [Required]
    public List<InboundItemInputDto> Items { get; set; } = new();
}

#endregion

#region 查询 DTO

/// <summary>入库单分页查询入参</summary>
public class InboundQueryDto
{
    public string? InboundNo { get; set; }
    public long? SupplierId { get; set; }
    public long? OrderId { get; set; }
    /// <summary>仓库ID筛选（当前系统无仓库主数据表，暂按ID精确匹配）</summary>
    public long? WarehouseId { get; set; }
    public int? Status { get; set; }
    public DateTime? InboundDateStart { get; set; }
    public DateTime? InboundDateEnd { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

#endregion

#region 出参 DTO

/// <summary>入库单列表项</summary>
public class InboundListDto
{
    public long Id { get; set; }
    public string InboundNo { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long OrderId { get; set; }
    public string? OrderNo { get; set; }
    public long WarehouseId { get; set; }
    public DateTime InboundDate { get; set; }
    public long? OperatorId { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>入库明细项（出参）</summary>
public class InboundDetailItemDto
{
    public long Id { get; set; }
    public long OrderItemId { get; set; }
    public long MaterialId { get; set; }
    public string? MaterialName { get; set; }
    /// <summary>采购单价（来自订单明细，便于核对）</summary>
    public decimal UnitPrice { get; set; }
    public decimal ActualQuantity { get; set; }
    public string? BatchNo { get; set; }
    public string? Remark { get; set; }
}

/// <summary>入库单详情</summary>
public class InboundDetailDto
{
    public long Id { get; set; }
    public string InboundNo { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string? OrderNo { get; set; }
    public long SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public long WarehouseId { get; set; }
    public DateTime InboundDate { get; set; }
    public long? OperatorId { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<InboundDetailItemDto> Items { get; set; } = new();
}

#endregion
