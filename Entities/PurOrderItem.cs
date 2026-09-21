using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 采购订单明细表 pur_order_item（采购管理模块）
///
/// 与 pur_order 一对多；一笔采购订单可含多种商品。
///
/// 【material_id】本系统库存以 SKU 为最小单位，故 material_id 实际关联 prod_sku.id
/// （prod_sku 上才有 stock 字段，入库确认时据此增加库存并写 log_stock_log）。
/// 如需改为"以商品(prod_info)为采购单位"，可将 material_id 改关联 prod_info.id，
/// 并在入库时按商品汇总更新其下所有 SKU 库存。
/// </summary>
[Table("pur_order_item")]
public class PurOrderItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>采购订单ID（关联 pur_order.id）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>物料/SKU ID（关联 prod_sku.id，本系统库存量单位）</summary>
    [Column("material_id")]
    public long MaterialId { get; set; }

    /// <summary>采购数量</summary>
    [Column("quantity", TypeName = "decimal(10,2)")]
    public decimal Quantity { get; set; }

    /// <summary>采购单价（元）</summary>
    [Column("unit_price", TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>行小计金额（数量 × 单价，服务端重算）</summary>
    [Column("subtotal", TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
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

    /// <summary>所属采购订单（查询时 Include）</summary>
    public PurOrder? Order { get; set; }
}
