using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 采购入库明细表 pur_inbound_item（采购管理模块）
///
/// 精确关联采购订单明细行（order_item_id），实现分批入库的精确追踪。
/// 每次入库时校验"已入库总数 + 本次入库数" 不超过订单采购总数，防止超量入库（规范 6.3）。
/// </summary>
[Table("pur_inbound_item")]
public class PurInboundItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>入库单ID（关联 pur_inbound.id）</summary>
    [Column("inbound_id")]
    public long InboundId { get; set; }

    /// <summary>关联采购订单明细ID（pur_order_item.id），精确记录对应订单哪一行</summary>
    [Column("order_item_id")]
    public long OrderItemId { get; set; }

    /// <summary>物料/SKU ID（冗余，便于快速查询；与 order_item.material_id 一致）</summary>
    [Column("material_id")]
    public long MaterialId { get; set; }

    /// <summary>实收数量</summary>
    [Column("actual_quantity", TypeName = "decimal(10,2)")]
    public decimal ActualQuantity { get; set; }

    /// <summary>入库批次号（可空）</summary>
    [StringLength(32)]
    [Column("batch_no")]
    public string? BatchNo { get; set; }

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

    /// <summary>所属入库单（查询时 Include）</summary>
    public PurInbound? Inbound { get; set; }

    /// <summary>关联的采购订单明细（查询时 Include）</summary>
    public PurOrderItem? OrderItem { get; set; }
}
