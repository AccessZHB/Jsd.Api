using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品入库单明细表 log_stock_in_item（依据 Jsd_order.sql 逐列映射）
/// 注意：该表只有 create_time，没有 update_time（编辑单据采用"删旧明细+插新明细"策略）。
/// </summary>
[Table("log_stock_in_item")]
public class LogStockInItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>入库单ID（关联 log_stock_in.id）</summary>
    [Column("stock_in_id")]
    public long StockInId { get; set; }

    /// <summary>
    /// 所属入库单导航属性。
    /// 关键修复：显式声明 [ForeignKey(nameof(StockInId))]，
    /// 让 EF Core 把 LogStockIn.Items 关系绑定到 StockInId 列（stock_in_id），
    /// 而不是按约定生成名为 LogStockInId 的「影子外键」列。
    /// 之前缺少此导航 + ForeignKey，导致 SaveChangesAsync 时 INSERT 语句包含
    /// 不存在的 LogStockInId 列，报 Unknown column 'LogStockInId'。
    /// </summary>
    [ForeignKey(nameof(StockInId))]
    public virtual LogStockIn? LogStockIn { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（可空；入库增加库存时必须指定具体 SKU）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>入库数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>入库成本价（用于后续计算利润）</summary>
    [Column("cost_price", TypeName = "decimal(12,2)")]
    public decimal CostPrice { get; set; }

    /// <summary>小计金额（数量 × 成本价，服务端计算）</summary>
    [Column("subtotal", TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    /// <summary>创建时间（数据库自动生成；本表无 update_time）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
