using System.Text.Json.Serialization;

namespace Jsd.Api.Models.Price;

/// <summary>
/// 分页查询入参公共契约（便于 Service 层统一归一化 Page / PageSize）
/// </summary>
public interface IPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    int Page { get; set; }

    /// <summary>每页条数（1~100）</summary>
    int PageSize { get; set; }
}

// ============================================================
// 一、客户（会员）等级 DTO
// ============================================================

/// <summary>客户等级列表/详情出参（mem_member_level）</summary>
public class MemberLevelDto
{
    public long Id { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    /// <summary>默认折扣率（0.8500 表示 85 折）</summary>
    public decimal DefaultDiscount { get; set; }

    public string? Description { get; set; }
    public int Status { get; set; }

    /// <summary>状态中文名（0-停用 1-启用）</summary>
    public string StatusText { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>新增客户等级入参（POST /api/member-levels）</summary>
public class CreateMemberLevelDto
{
    public string LevelName { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal DefaultDiscount { get; set; } = 1.0000m;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>更新客户等级入参（PUT /api/member-levels/{id}）</summary>
public class UpdateMemberLevelDto
{
    public string LevelName { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal DefaultDiscount { get; set; } = 1.0000m;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>启用/停用等级入参（PATCH /api/member-levels/{id}/status）</summary>
public class MemberLevelStatusDto
{
    /// <summary>目标状态：0-停用 1-启用</summary>
    public int Status { get; set; }
}

/// <summary>客户等级分页查询入参（GET /api/member-levels）</summary>
public class MemberLevelQueryDto : IPagedQuery
{
    /// <summary>状态筛选：0-停用 1-启用（不传=全部）</summary>
    public int? Status { get; set; }

    /// <summary>等级名称/编码模糊查询</summary>
    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ============================================================
// 二、价格策略 DTO
// ============================================================

/// <summary>价格策略列表/详情出参（price_strategy）</summary>
public class PriceStrategyDto
{
    public long Id { get; set; }
    public string StrategyNo { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>策略类型：1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价</summary>
    public int StrategyType { get; set; }

    /// <summary>策略类型中文名</summary>
    public string StrategyTypeText { get; set; } = string.Empty;

    public int Priority { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpireDate { get; set; }

    /// <summary>状态：0-草稿 1-启用 2-停用</summary>
    public int Status { get; set; }

    /// <summary>状态中文名</summary>
    public string StatusText { get; set; } = string.Empty;

    public string? Description { get; set; }
    public long CreateBy { get; set; }

    /// <summary>创建人账号（批量补全，便于列表展示）</summary>
    public string? CreateByName { get; set; }

    /// <summary>规则条数（提交启用时用于完整性校验提示）</summary>
    public int RuleCount { get; set; }

    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>新增价格策略入参（POST /api/price-strategies，默认创建为草稿）</summary>
public class CreatePriceStrategyDto
{
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>策略类型：1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价</summary>
    public int StrategyType { get; set; }

    public int Priority { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>更新价格策略入参（PUT /api/price-strategies/{id}，仅草稿可改）</summary>
public class UpdatePriceStrategyDto
{
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>策略类型（草稿阶段允许调整）</summary>
    public int StrategyType { get; set; }

    public int Priority { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 商品物料下拉选项（GET /api/price/materials）
/// material_id 口径 = prod_sku.id（与 price_rule_item.material_id、取价引擎一致）。
/// </summary>
public class MaterialOptionDto
{
    /// <summary>物料ID（prod_sku.id）</summary>
    public long MaterialId { get; set; }

    /// <summary>商品ID（prod_info.id）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称</summary>
    public string? ProdName { get; set; }

    /// <summary>SKU 名称</summary>
    public string? SkuName { get; set; }

    /// <summary>SKU 编码</summary>
    public string? SkuCode { get; set; }

    /// <summary>标准售价（prod_sku.retail_price）</summary>
    public decimal RetailPrice { get; set; }

    /// <summary>所属分类ID（配置"指定分类"规则时可用于提示）</summary>
    public long ProdCategoryId { get; set; }
}

/// <summary>停用策略入参（PATCH /api/price-strategies/{id}/disable）</summary>
public class DisableStrategyDto
{
    /// <summary>停用原因（写入 price_change_log.reason）</summary>
    public string? Reason { get; set; }
}

/// <summary>价格策略分页查询入参（GET /api/price-strategies）</summary>
public class PriceStrategyQueryDto : IPagedQuery
{
    /// <summary>策略类型筛选</summary>
    public int? StrategyType { get; set; }

    /// <summary>状态筛选：0-草稿 1-启用 2-停用</summary>
    public int? Status { get; set; }

    /// <summary>策略编号/名称模糊查询</summary>
    public string? Keyword { get; set; }

    /// <summary>生效日期范围（按 effective_date 过滤）</summary>
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ============================================================
// 三、价格规则 DTO（批量维护为全量覆盖）
// ============================================================

/// <summary>价格规则出参（price_rule + 明细）</summary>
public class PriceRuleDto
{
    public long Id { get; set; }
    public long StrategyId { get; set; }

    /// <summary>会员ID（专属价）</summary>
    public long? MemMemberId { get; set; }

    /// <summary>会员名称（批量补全）</summary>
    public string? MemMemberName { get; set; }

    /// <summary>等级ID（等级价）</summary>
    public long? CustomerLevelId { get; set; }

    /// <summary>等级名称（批量补全）</summary>
    public string? CustomerLevelName { get; set; }

    /// <summary>适用范围：1-全部 2-分类 3-指定商品</summary>
    public int ApplyScope { get; set; }

    /// <summary>适用范围中文名</summary>
    public string ApplyScopeText { get; set; } = string.Empty;

    public long? CategoryId { get; set; }

    /// <summary>分类名称（批量补全）</summary>
    public string? CategoryName { get; set; }

    /// <summary>计算方式：1-固定价 2-折扣率 3-减免金额</summary>
    public int CalcType { get; set; }

    /// <summary>计算方式中文名</summary>
    public string CalcTypeText { get; set; } = string.Empty;

    public decimal CalcValue { get; set; }

    /// <summary>底价保护</summary>
    public decimal? MinPrice { get; set; }

    public int Status { get; set; }

    /// <summary>明细行（指定商品 / 阶梯价）</summary>
    public List<PriceRuleItemDto> Items { get; set; } = new();

    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>价格规则明细出参（price_rule_item）</summary>
public class PriceRuleItemDto
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public long MaterialId { get; set; }

    /// <summary>商品名称（批量补全）</summary>
    public string? MaterialName { get; set; }

    /// <summary>阶梯序号</summary>
    public int TierNo { get; set; }

    public int MinQuantity { get; set; }

    /// <summary>最大数量（0/空=不封顶）</summary>
    public int? MaxQuantity { get; set; }

    public decimal? Price { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ReduceAmount { get; set; }
}

/// <summary>批量维护规则明细入参</summary>
public class PriceRuleItemInputDto
{
    /// <summary>商品物料ID（prod_sku.id）</summary>
    public long MaterialId { get; set; }

    public int TierNo { get; set; } = 1;
    public int MinQuantity { get; set; }

    /// <summary>最大数量（0/空=不封顶）</summary>
    public int? MaxQuantity { get; set; }

    public decimal? Price { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ReduceAmount { get; set; }
}

/// <summary>批量维护规则入参</summary>
public class PriceRuleInputDto
{
    /// <summary>规则ID（&gt;0 表示更新已有规则；0/null 表示新增）</summary>
    public long? Id { get; set; }

    public long? MemMemberId { get; set; }
    public long? CustomerLevelId { get; set; }

    /// <summary>适用范围：1-全部 2-分类 3-指定商品</summary>
    public int ApplyScope { get; set; } = (int)Jsd.Api.Models.Price.ApplyScope.All;

    public long? CategoryId { get; set; }

    /// <summary>计算方式：1-固定价 2-折扣率 3-减免金额</summary>
    public int CalcType { get; set; } = (int)Jsd.Api.Models.Price.CalcType.Discount;

    public decimal CalcValue { get; set; }
    public decimal? MinPrice { get; set; }
    public int Status { get; set; } = 1;

    public List<PriceRuleItemInputDto> Items { get; set; } = new();
}

/// <summary>
/// 批量维护规则及明细入参（POST /api/price-strategies/{id}/rules/batch）
/// 【全量覆盖】以本次提交的规则集合为准：缺失的旧规则被停用（记变更日志），新增/修改的逐条落库。
/// </summary>
public class PriceRuleBatchSaveDto
{
    public List<PriceRuleInputDto> Rules { get; set; } = new();

    /// <summary>变更原因（写入 price_change_log.reason）</summary>
    public string? Reason { get; set; }
}

// ============================================================
// 四、价格变更日志 DTO
// ============================================================

/// <summary>价格变更日志出参（price_change_log）</summary>
public class PriceChangeLogDto
{
    public long Id { get; set; }
    public long? StrategyId { get; set; }
    public string? StrategyName { get; set; }
    public long? RuleId { get; set; }

    /// <summary>操作类型：1-新增 2-修改 3-停用 4-删除</summary>
    public int OperationType { get; set; }

    /// <summary>操作类型中文名</summary>
    public string OperationTypeText { get; set; } = string.Empty;

    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public long? OperatorId { get; set; }

    /// <summary>操作人账号（批量补全）</summary>
    public string? OperatorName { get; set; }

    public DateTime OperateTime { get; set; }
    public string? Reason { get; set; }
}

/// <summary>价格变更日志分页查询入参（GET /api/price-change-logs）</summary>
public class PriceChangeLogQueryDto : IPagedQuery
{
    public long? StrategyId { get; set; }
    public long? RuleId { get; set; }
    public int? OperationType { get; set; }

    /// <summary>策略编号/名称模糊查询（日志页"策略名称/编号"搜索框）</summary>
    public string? Keyword { get; set; }

    /// <summary>操作人账号模糊查询</summary>
    public string? OperatorName { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ============================================================
// 五、订单价格快照 DTO（内部接口，下单时批量写入）
// ============================================================

/// <summary>订单价格快照出参（order_price_snapshot）</summary>
public class OrderPriceSnapshotDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>订单号（列表联查 trx_order 补全）</summary>
    public string? OrderNo { get; set; }

    public long OrderItemId { get; set; }
    public long MaterialId { get; set; }

    /// <summary>商品/SKU 名称（列表联查补全）</summary>
    public string? MaterialName { get; set; }

    /// <summary>下单客户（列表联查补全）</summary>
    public string? MemberName { get; set; }

    public decimal StandardPrice { get; set; }
    public long? MatchedStrategyId { get; set; }

    /// <summary>命中策略名称（列表联查 price_strategy 补全）</summary>
    public string? MatchedStrategyName { get; set; }

    public int? MatchedStrategyType { get; set; }
    public string? MatchedStrategyTypeText { get; set; }
    public long? MatchedRuleId { get; set; }
    public decimal FinalPrice { get; set; }
    public string? DiscountInfo { get; set; }

    /// <summary>下单时间（按订单创建时间，供时间范围筛选与展示）</summary>
    public DateTime? OrderTime { get; set; }
}

/// <summary>订单价格快照分页查询入参（GET /api/order-price-snapshots）</summary>
public class OrderPriceSnapshotQueryDto : IPagedQuery
{
    /// <summary>订单号（模糊）</summary>
    public string? OrderNo { get; set; }

    /// <summary>客户名称（模糊）</summary>
    public string? MemberName { get; set; }

    /// <summary>商品名称 / SKU 名称（模糊）</summary>
    public string? MaterialName { get; set; }

    /// <summary>命中策略类型：1-等级价 2-专属价 3-阶梯价 4-促销价（0 表示未命中）</summary>
    public int? MatchedStrategyType { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>批量写入价格快照入参（POST /api/order-price-snapshots/batch）</summary>
public class OrderPriceSnapshotBatchDto
{
    /// <summary>订单ID</summary>
    public long OrderId { get; set; }

    /// <summary>快照明细（逐条对应订单明细）</summary>
    public List<OrderPriceSnapshotInputDto> Items { get; set; } = new();
}

/// <summary>单条价格快照入参</summary>
public class OrderPriceSnapshotInputDto
{
    public long OrderItemId { get; set; }
    public long MaterialId { get; set; }
    public decimal StandardPrice { get; set; }
    public long? MatchedStrategyId { get; set; }
    public int? MatchedStrategyType { get; set; }
    public long? MatchedRuleId { get; set; }
    public decimal FinalPrice { get; set; }

    /// <summary>折扣详情（JSON 字符串；对象入参时由后端序列化）</summary>
    [JsonPropertyName("discountInfo")]
    public string? DiscountInfo { get; set; }
}
