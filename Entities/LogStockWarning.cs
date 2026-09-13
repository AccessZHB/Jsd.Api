using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 库存预警配置表 log_stock_warning（依据 Jsd_order.sql 第 452 行建表语句逐列映射）。
///
/// 设计要点：
/// 1. 本表只存"预警配置"，不存预警状态；是否触发预警由 Service 实时计算
///    （Status=1 且 当前库存 &lt;= WarningStock），因此无需额外的预警状态列与定时任务；
/// 2. sku_id 可空：为空表示【商品级预警】（按该商品全部 SKU 库存合计判定），
///    有值表示【SKU 级预警】（按该 SKU 库存判定）；
/// 3. status 是配置本身的启用/停用开关（1-启用 0-停用），与"是否触发预警"是两个概念：
///    停用后即使库存低于阈值也不再预警。
/// </summary>
[Table("log_stock_warning")]
public class LogStockWarning
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>商品ID（关联 prod_info.id，NOT NULL）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（关联 prod_sku.id，可为 NULL = 商品级预警）</summary>
    [Column("sku_id")]
    public long? SkuId { get; set; }

    /// <summary>预警阈值（库存 &lt;= 此值时报警，默认 10）</summary>
    [Column("warning_stock")]
    public int WarningStock { get; set; } = 10;

    /// <summary>状态：1-启用 0-停用（默认启用）</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
