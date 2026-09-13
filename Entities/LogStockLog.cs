using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 库存变动日志表 log_stock_log（审计追溯，依据 Jsd_order.sql 逐列映射）
/// 注意：change_type 在数据库中是 TINYINT（1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他），
/// 业务来源单据类型（stock_in / stock_out / check / order）存放在 ref_type 字符串列。
/// </summary>
[Table("log_stock_log")]
public class LogStockLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>商品ID（SPU，关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（null/0=不区分SKU，作用于整个商品）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>变动类型：1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他</summary>
    [Column("change_type")]
    public int ChangeType { get; set; }

    /// <summary>变动数量（正为增加，负为减少）</summary>
    [Column("change_qty")]
    public int ChangeQty { get; set; }

    /// <summary>变动前库存</summary>
    [Column("before_stock")]
    public int BeforeStock { get; set; }

    /// <summary>变动后库存</summary>
    [Column("after_stock")]
    public int AfterStock { get; set; }

    /// <summary>关联类型：stock_in / stock_out / check / order（编辑入库单用 stock_in_update 标识）</summary>
    [StringLength(20)]
    [Column("ref_type")]
    public string? RefType { get; set; }

    /// <summary>关联单据ID（如入库单 log_stock_in.id）</summary>
    [Column("ref_id")]
    public long? RefId { get; set; }

    /// <summary>备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>操作人</summary>
    [Required]
    [StringLength(50)]
    [Column("operator")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>创建时间（数据库自动生成；本表无 update_time）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }
}
