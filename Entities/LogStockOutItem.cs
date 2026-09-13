using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品出库单明细表 log_stock_out_item（依据 Jsd_order.sql 逐列映射）
/// 注意：该表只有 create_time，没有 update_time（编辑单据采用"删旧明细+插新明细"策略）。
/// 出库只扣减具体 SKU 库存（prod_sku.stock），因此 sku_id 必填。
/// </summary>
[Table("log_stock_out_item")]
public class LogStockOutItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>出库单ID（关联 log_stock_out.id）</summary>
    [Column("stock_out_id")]
    public long StockOutId { get; set; }

    /// <summary>
    /// 所属出库单导航属性。
    /// 显式声明 [ForeignKey(nameof(StockOutId))]，让 EF Core 把 LogStockOut.Items 关系
    /// 绑定到 StockOutId 列（stock_out_id），而不是按约定生成名为 LogStockOutId 的
    /// 「影子外键」列（否则 SaveChangesAsync 会报 Unknown column 'LogStockOutId'）。
    /// </summary>
    [ForeignKey(nameof(StockOutId))]
    public virtual LogStockOut? LogStockOut { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（必填；出库扣减该 SKU 的库存）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>出库数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>明细备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成；本表无 update_time）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
