using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 采购订单主表 pur_order（采购管理模块）
///
/// 【状态流转 status】本模块【无审核环节】
///   0-草稿 Draft ──确认订单──&gt; 2-待入库 Approved
///   2 ──入库确认(部分)──&gt; 3-部分入库 PartialIn
///   2 ──入库确认(全部)──&gt; 4-已结清/已完成 Completed
///   0 ──作废──&gt; 9-作废 Cancelled
///
/// 【关于 1-审批中】系统不做审批，该状态仅作为历史数据兼容保留（枚举仍定义，但不主动置为该值）。
///
/// 【material_id 说明】本系统库存以 SKU 为最小单位（prod_sku.stock），
/// 因此采购订单明细的 material_id 实际关联 prod_sku.id（见 PurOrderItem）。
///
/// 【软删除】不使用物理删除，仅通过 status=9 标记作废（见规范 6.7）。
/// </summary>
[Table("pur_order")]
public class PurOrder
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>采购单号（唯一，格式 PO-yyyyMMdd-001，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("order_no")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>供应商ID（关联 sup_supplier.id）</summary>
    [Column("supplier_id")]
    public long SupplierId { get; set; }

    /// <summary>下单日期（DATE）</summary>
    [Column("order_date")]
    public DateTime OrderDate { get; set; }

    /// <summary>预计到货日期（可空）</summary>
    [Column("expected_delivery_date")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>状态：0-草稿 1-审批中(历史兼容) 2-待入库 3-部分入库 4-已结清 9-作废</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>订单总金额（元，= Σ明细小计）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    // ---------- 导航属性（仅用于查询时 Include，不配置物理外键级联） ----------
    /// <summary>供应商（查询时 Include）</summary>
    public SupSupplier? Supplier { get; set; }

    /// <summary>采购订单明细集合</summary>
    public List<PurOrderItem> Items { get; set; } = new();
}
