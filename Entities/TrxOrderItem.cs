using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 订单明细表 trx_order_item（依据 Jsd_order.sql 第 521 行建表语句逐列映射）
///
/// 【快照设计（重要）】
/// prod_name / sku_name / spec_values / product_image / unit_price 全部是【下单瞬间的快照】：
/// 商品后续改价、改名、改图都不会影响历史订单，保证订单数据可追溯、金额可对账。
/// 因此创建订单时必须从 prod_info / prod_sku 实时读取并写入这些字段，不能留空。
///
/// 【外键规范】严格使用 prod_info_id / sku_id（与项目其他模块一致），禁止臆造字段。
///
/// 【注意】反向导航属性 Order + [ForeignKey(nameof(OrderId))] 是必须的：
/// 若只有主表有集合导航、明细表没有反向导航，EF Core 会按约定生成名为 TrxOrderId 的
/// "影子外键"列，插入时引用不存在的列报错（本项目入库模块曾踩过此坑）。
/// </summary>
[Table("trx_order_item")]
public class TrxOrderItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>订单ID（关联 trx_order.id）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>商品ID（关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（关联 prod_sku.id；无 SKU 时存 0）</summary>
    [Column("sku_id")]
    public long SkuId { get; set; }

    /// <summary>商品名称（快照，商品改名不影响历史订单）</summary>
    [StringLength(200)]
    [Column("prod_name")]
    public string ProdName { get; set; } = string.Empty;

    /// <summary>SKU 名称（快照）</summary>
    [StringLength(200)]
    [Column("sku_name")]
    public string SkuName { get; set; } = string.Empty;

    /// <summary>规格值（快照，如：红色/L码）</summary>
    [StringLength(500)]
    [Column("spec_values")]
    public string SpecValues { get; set; } = string.Empty;

    /// <summary>商品图片（快照）</summary>
    [StringLength(500)]
    [Column("product_image")]
    public string ProductImage { get; set; } = string.Empty;

    /// <summary>单价（下单时的价格，快照）</summary>
    [Column("unit_price", TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>购买数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>小计金额 = 单价 × 数量</summary>
    [Column("subtotal_amount", TypeName = "decimal(12,2)")]
    public decimal SubtotalAmount { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// 所属订单（反向导航）。
    /// [ForeignKey(nameof(OrderId))] 把关系显式绑定到已有列 order_id，避免 EF 生成影子外键。
    /// </summary>
    [ForeignKey(nameof(OrderId))]
    public virtual TrxOrder? Order { get; set; }
}
