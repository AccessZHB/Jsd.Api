using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 订单价格快照表 order_price_snapshot（价格策略与会员余额模块）
///
/// 【核心原则：价格计算必须可追溯】
///   下单瞬间把「标准售价 → 命中策略 → 命中规则 → 最终成交价 → 折扣详情」整条链路冻结入库。
///   之后商品改价、策略停用、等级调整都不允许影响历史订单，
///   本表是历史价格追溯与毛利分析的【唯一依据】。
///
/// 【写入时机】订单主表与明细落库后立即批量写入（PriceService.SaveSnapshotsAsync），
///   与订单创建处于同一事务，不允许异步补偿。
/// </summary>
[Table("order_price_snapshot")]
public class OrderPriceSnapshot
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>订单ID（trx_order.id）⚠️ 数据库列名为 trx_order_id</summary>
    [Column("trx_order_id")]
    public long OrderId { get; set; }

    /// <summary>订单明细ID（trx_order_item.id）⚠️ 数据库列名为 trx_order_item_id</summary>
    [Column("trx_order_item_id")]
    public long OrderItemId { get; set; }

    /// <summary>商品ID（prod_info.id，数据库该列 NOT NULL）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>
    /// SKU ID（prod_sku.id）—— 即前端与取价引擎口径的 materialId。
    /// ⚠️ 数据库列名为 sku_id；C# 属性名保留 MaterialId 以保持 DTO/前端 JSON 不变。
    /// </summary>
    [Column("sku_id")]
    public long MaterialId { get; set; }

    /// <summary>标准售价（取价时的 prod_sku.retail_price）</summary>
    [Column("standard_price", TypeName = "decimal(10,2)")]
    public decimal StandardPrice { get; set; }

    /// <summary>命中策略ID（未命中填 0）</summary>
    [Column("matched_strategy_id")]
    public long? MatchedStrategyId { get; set; }

    /// <summary>命中策略类型：1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价（未命中填 0）</summary>
    [Column("matched_strategy_type")]
    public int? MatchedStrategyType { get; set; }

    /// <summary>命中规则ID（未命中填 0）</summary>
    [Column("matched_rule_id")]
    public long? MatchedRuleId { get; set; }

    /// <summary>最终成交价（单价，底价保护不拦截，仅记录）</summary>
    [Column("final_price", TypeName = "decimal(10,2)")]
    public decimal FinalPrice { get; set; }

    /// <summary>
    /// 折扣详情快照（JSON 字符串：策略名/规则/计算方式/计算值/是否触发底价保护等）。
    /// ⚠️ 数据库该列是 VARCHAR(1000)（原建表为 VARCHAR(255)，已加宽），不是 JSON 类型，
    ///    因此这里按 string 映射；写入前由 PriceService.SerializeDiscountInfo 序列化为 JSON 文本。
    /// </summary>
    [Column("discount_info")]
    public string? DiscountInfo { get; set; }
}
