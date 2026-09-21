using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 退货明细表 trx_return_item（售后模块）
///
/// 精确记录每个退回商品的名称、规格、数量、单价及小计（下单时的快照）。
/// 仓库收货时校验"实收数量 ≤ 申请退货数量"，收货后按 sku_id 回滚库存。
///
/// 【状态 status】0-待确认 1-已收货 2-已拒收
/// </summary>
[Table("trx_return_item")]
public class TrxReturnItem
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>退货单ID（关联 trx_return.id）</summary>
    [Column("return_id")]
    public long ReturnId { get; set; }

    /// <summary>关联订单明细ID（trx_order_item.id，可空）</summary>
    [Column("order_item_id")]
    public long? OrderItemId { get; set; }

    /// <summary>商品ID（关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（关联 prod_sku.id，库存回滚用）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>商品名称（退货时快照）</summary>
    [Required]
    [StringLength(100)]
    [Column("prod_info_name")]
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>SKU名称（退货时快照）</summary>
    [StringLength(100)]
    [Column("sku_name")]
    public string? SkuName { get; set; }

    /// <summary>申请退货数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>实收数量（仓库收货时写入；默认 0，≤ 申请数量；0 表示拒收）</summary>
    [Column("received_quantity")]
    public int ReceivedQuantity { get; set; }

    /// <summary>商品单价（元，退货时快照）</summary>
    [Column("unit_price", TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>退货小计金额（数量 × 单价）</summary>
    [Column("subtotal", TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    /// <summary>状态：0-待确认 1-已收货 2-已拒收</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>所属退货单（查询时 Include）</summary>
    public TrxReturn? Return { get; set; }
}
