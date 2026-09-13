using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 库存盘点单明细表 log_stock_check_item（依据 Jsd_order.sql 逐列映射）
/// 系统库存(system_stock)在创建盘点单时从 prod_sku.stock 快照；
/// 实盘(actual_stock)在"完成盘点"时录入；diff_qty = actual_stock - system_stock（盈亏）。
/// 注意：反向导航 + [ForeignKey] 避免 EF 生成名为 LogStockCheckId 的影子外键列。
/// </summary>
[Table("log_stock_check_item")]
public class LogStockCheckItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>盘点单ID（关联 log_stock_check.id）</summary>
    [Column("check_id")]
    public long CheckId { get; set; }

    /// <summary>
    /// 所属盘点单导航属性。
    /// 显式声明 [ForeignKey(nameof(CheckId))]，把 LogStockCheck.Items 关系绑定到
    /// CheckId 列（check_id），避免 EF 按约定生成名为 LogStockCheckId 的影子外键列。
    /// </summary>
    [ForeignKey(nameof(CheckId))]
    public virtual LogStockCheck? LogStockCheck { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（必填；盘点精确到具体 SKU）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>系统库存（盘点前快照，创建时从 prod_sku.stock 读取）</summary>
    [Column("system_stock")]
    public int SystemStock { get; set; }

    /// <summary>实盘数量（盘点时录入；盘点中为 0）</summary>
    [Column("actual_stock")]
    public int ActualStock { get; set; }

    /// <summary>盈亏数量 = actual_stock - system_stock（正为盈、负为亏）</summary>
    [Column("diff_qty")]
    public int DiffQty { get; set; }

    /// <summary>差异原因/备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成；本表无 update_time）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
