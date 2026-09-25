namespace Jsd.Api.Models.Price;

/// <summary>
/// 取价请求入参（POST /api/price/quotation）
/// 入参：会员ID + 商品清单（物料ID + 数量）。
/// </summary>
public class QuotationRequestDto
{
    /// <summary>会员ID（mem_member.id；用于匹配客户专属价 / 客户等级价）</summary>
    public long MemMemberId { get; set; }

    /// <summary>商品清单（至少一条）</summary>
    public List<QuotationItemInputDto> Items { get; set; } = new();
}

/// <summary>取价商品行入参</summary>
public class QuotationItemInputDto
{
    /// <summary>商品物料ID（prod_sku.id）</summary>
    public long MaterialId { get; set; }

    /// <summary>购买数量（阶梯价按此数量命中阶梯）</summary>
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// 取价结果出参（每个商品一行）
/// 【可追溯】命中策略 / 命中规则 / 计算方式 / 计算值 / 折扣详情全部回传，
/// 前端可直接展示"为什么是这个价"，下单时原样写入 order_price_snapshot。
/// </summary>
public class QuotationResultDto
{
    /// <summary>商品物料ID（prod_sku.id）</summary>
    public long MaterialId { get; set; }

    /// <summary>商品ID（prod_info.id）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称</summary>
    public string? MaterialName { get; set; }

    /// <summary>SKU 名称</summary>
    public string? SkuName { get; set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; set; }

    /// <summary>标准售价（prod_sku.retail_price）</summary>
    public decimal StandardPrice { get; set; }

    /// <summary>最终成交价（单价，2 位小数）</summary>
    public decimal FinalPrice { get; set; }

    /// <summary>小计金额 = 最终成交价 × 数量</summary>
    public decimal Subtotal { get; set; }

    /// <summary>命中策略ID（未命中填 0 / null）</summary>
    public long? MatchedStrategyId { get; set; }

    /// <summary>命中策略编号</summary>
    public string? MatchedStrategyNo { get; set; }

    /// <summary>命中策略名称</summary>
    public string? MatchedStrategyName { get; set; }

    /// <summary>命中策略类型（1-等级价 2-专属价 3-阶梯价 4-促销价；0=未命中）</summary>
    public int MatchedStrategyType { get; set; }

    /// <summary>命中策略类型中文名</summary>
    public string MatchedStrategyTypeText { get; set; } = "标准售价";

    /// <summary>命中规则ID</summary>
    public long? MatchedRuleId { get; set; }

    /// <summary>命中阶梯序号（阶梯价场景；0=非阶梯）</summary>
    public int? MatchedTierNo { get; set; }

    /// <summary>计算方式：1-固定价 2-折扣率 3-减免金额（0=未命中走标准价）</summary>
    public int CalcType { get; set; }

    /// <summary>计算方式中文名</summary>
    public string CalcTypeText { get; set; } = string.Empty;

    /// <summary>计算值（固定价=单价；折扣率=0.8500；减免=金额）</summary>
    public decimal? CalcValue { get; set; }

    /// <summary>底价保护值</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// 是否触发底价保护：最终价低于 min_price 时为 true。
    /// 【不拦截】仅打标志，由前端或订单服务决定是否转人工审批。
    /// </summary>
    public bool IsBelowMinPrice { get; set; }

    /// <summary>折扣详情（结构化，前端可直接渲染；同时原样序列化进快照）</summary>
    public DiscountInfoDto DiscountInfo { get; set; } = new();
}

/// <summary>折扣详情（写入 order_price_snapshot.discount_info 的 JSON 结构）</summary>
public class DiscountInfoDto
{
    /// <summary>命中策略ID</summary>
    public long? StrategyId { get; set; }

    /// <summary>命中策略名称</summary>
    public string? StrategyName { get; set; }

    /// <summary>命中策略类型</summary>
    public int StrategyType { get; set; }

    /// <summary>命中策略类型中文名</summary>
    public string? StrategyTypeText { get; set; }

    /// <summary>命中规则ID</summary>
    public long? RuleId { get; set; }

    /// <summary>命中阶梯序号</summary>
    public int? TierNo { get; set; }

    /// <summary>计算方式中文名</summary>
    public string? CalcTypeText { get; set; }

    /// <summary>计算值</summary>
    public decimal? CalcValue { get; set; }

    /// <summary>标准售价</summary>
    public decimal StandardPrice { get; set; }

    /// <summary>最终成交价</summary>
    public decimal FinalPrice { get; set; }

    /// <summary>优惠金额（标准售价 - 最终成交价）</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>是否触发底价保护</summary>
    public bool IsBelowMinPrice { get; set; }

    /// <summary>底价保护阈值</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>取价说明（如：客户专属价-折扣率 0.8500）</summary>
    public string? Remark { get; set; }
}
