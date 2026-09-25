using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 价格规则明细表 price_rule_item（价格策略与会员余额模块）
///
/// 双重职责：
///   1）指定商品清单（apply_scope=3 时，material_id 即允许使用该规则的商品 SKU）；
///   2）批量阶梯价（apply_scope 任意、calc_type 走阶梯）：
///      按 min_quantity ≤ 下单数量 ≤ max_quantity 命中阶梯行（tier_no 升序优先），
///      命中后优先取 price / discount / reduce_amount，未配置时回退到规则的 calc_value。
///
/// material_id 关联 prod_sku.id（与采购模块 material_id 口径一致）。
/// </summary>
[Table("price_rule_item")]
public class PriceRuleItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联规则ID（price_rule.id）</summary>
    [Column("rule_id")]
    public long RuleId { get; set; }

    /// <summary>
    /// 商品ID（关联 prod_info.id）。
    /// ⚠️ 数据库该列为 NOT NULL（见 Jsd_order.sql），保存明细时必须按 SKU 反查商品ID后一并写入。
    /// </summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>
    /// SKU ID（关联 prod_sku.id）——即取价引擎与前端下拉口径里的 material_id。
    /// ⚠️ 数据库列名是 sku_id，不是需求文档里的 material_id；C# 属性名保留 MaterialId
    ///    是为了让 DTO / 前端 JSON 字段（materialId）保持不变，避免前后端契约改动。
    /// </summary>
    [Column("sku_id")]
    public long MaterialId { get; set; }

    /// <summary>阶梯序号（从 1 开始；非阶梯场景填 1）</summary>
    [Column("tier_no")]
    public int TierNo { get; set; } = 1;

    /// <summary>最小数量（含）</summary>
    [Column("min_quantity")]
    public int MinQuantity { get; set; }

    /// <summary>最大数量（含；0 或 NULL 表示不封顶）</summary>
    [Column("max_quantity")]
    public int? MaxQuantity { get; set; }

    /// <summary>阶梯单价（calc_type=1 固定价时使用）</summary>
    [Column("price", TypeName = "decimal(10,2)")]
    public decimal? Price { get; set; }

    /// <summary>阶梯折扣（calc_type=2 折扣率时使用，decimal(5,4)）</summary>
    [Column("discount", TypeName = "decimal(5,4)")]
    public decimal? Discount { get; set; }

    /// <summary>阶梯减免金额（calc_type=3 减免金额时使用）</summary>
    [Column("reduce_amount", TypeName = "decimal(10,2)")]
    public decimal? ReduceAmount { get; set; }

    /// <summary>所属规则</summary>
    public virtual PriceRule? Rule { get; set; }
}
