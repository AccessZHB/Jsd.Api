using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 价格规则表 price_rule（价格策略与会员余额模块）
///
/// 一条策略下可有多条规则，规则决定「对谁生效、对哪些商品生效、怎么算钱」：
///   ● 对谁生效：客户专属价填 mem_member_id；客户等级价填 customer_level_id；其余类型两者留空。
///   ● 对哪些商品生效：apply_scope 1-全部商品 2-指定分类（填 category_id） 3-指定商品（见 price_rule_item）。
///   ● 怎么算钱：calc_type 1-固定价 2-折扣率 3-减免金额，计算值取 calc_value；
///     批量阶梯价优先取 price_rule_item 中的阶梯值（price / discount / reduce_amount）。
///   ● 底价保护 min_price：计算出的最终价低于该值时不拦截，仅回传 is_below_min_price 标志。
/// </summary>
[Table("price_rule")]
public class PriceRule
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联策略ID（price_strategy.id）</summary>
    [Column("strategy_id")]
    public long StrategyId { get; set; }

    /// <summary>会员ID（客户专属价时填写，其余类型留空）</summary>
    [Column("mem_member_id")]
    public long? MemMemberId { get; set; }

    /// <summary>等级ID（客户等级价时填写，关联 mem_member_level.id）</summary>
    [Column("customer_level_id")]
    public long? CustomerLevelId { get; set; }

    /// <summary>适用范围：1-全部商品 2-指定分类 3-指定商品</summary>
    [Column("apply_scope")]
    public int ApplyScope { get; set; }

    /// <summary>分类ID（apply_scope=2 时填写，关联 prod_category.id）</summary>
    [Column("category_id")]
    public long? CategoryId { get; set; }

    /// <summary>计算方式：1-固定价 2-折扣率 3-减免金额</summary>
    [Column("calc_type")]
    public int CalcType { get; set; }

    /// <summary>计算值（固定价=单价；折扣率=0.8500 表示 85 折；减免金额=立减元）</summary>
    [Column("calc_value", TypeName = "decimal(10,2)")]
    public decimal CalcValue { get; set; }

    /// <summary>底价保护（最终价低于此值时仅打标不拦截）</summary>
    [Column("min_price", TypeName = "decimal(10,2)")]
    public decimal? MinPrice { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
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

    /// <summary>所属策略</summary>
    public virtual PriceStrategy? Strategy { get; set; }

    /// <summary>规则明细（指定商品 / 阶梯价）</summary>
    public virtual List<PriceRuleItem> Items { get; set; } = new();
}
