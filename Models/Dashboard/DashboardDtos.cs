namespace Jsd.Api.Models.Dashboard;

/// <summary>
/// 数据看板相关响应 DTO。
/// 设计约定：所有数值字段用 0 而非 null，避免前端图表出现 NaN / 空白。
/// 金额单位：元（decimal，数据库 decimal(12,2) 直接透传）。
/// </summary>

/// <summary>核心概览卡片（4 项）</summary>
public class DashboardOverviewDto
{
    /// <summary>销售额（已支付订单金额总和，扣除已退款金额）</summary>
    public decimal SalesAmount { get; set; }

    /// <summary>订单数（已支付订单数量）</summary>
    public int OrderCount { get; set; }

    /// <summary>客单价 = 销售额 / 订单数（订单数为 0 时返回 0）</summary>
    public decimal AvgOrderValue { get; set; }

    /// <summary>退款金额（已退款单的金额合计）</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>退款单数（已退款单数量）</summary>
    public int RefundCount { get; set; }
}

/// <summary>销售趋势单个数据点（用于折线图横坐标/纵坐标）</summary>
public class DashboardTrendPointDto
{
    /// <summary>横坐标标签（日：MM/dd；周：MM/dd(周一)；月：yyyy-MM）</summary>
    public string DateLabel { get; set; } = string.Empty;

    /// <summary>该周期销售额（已扣除退款）</summary>
    public decimal SalesAmount { get; set; }

    /// <summary>该周期订单数</summary>
    public int OrderCount { get; set; }
}

/// <summary>销售趋势（按 day/week/month 聚合）</summary>
public class DashboardTrendDto
{
    /// <summary>聚合粒度：day / week / month</summary>
    public string Granularity { get; set; } = "day";

    /// <summary>横坐标数组（已按时间连续补齐，无数据的周期补 0）</summary>
    public List<DashboardTrendPointDto> Points { get; set; } = new();
}

/// <summary>商品销量排行单条</summary>
public class DashboardTopProductDto
{
    /// <summary>商品ID</summary>
    public long ProdInfoId { get; set; }

    /// <summary>商品名称（取下单快照 prod_name）</summary>
    public string ProdName { get; set; } = string.Empty;

    /// <summary>销量（明细 quantity 之和）</summary>
    public int Quantity { get; set; }

    /// <summary>销售额（明细 subtotal_amount 之和）</summary>
    public decimal SalesAmount { get; set; }
}

/// <summary>订单状态分布单条（前端按 Status 映射文案）</summary>
public class DashboardOrderStatusDto
{
    /// <summary>订单状态码：0-待付款 1-已支付(待发货) 2-已发货(待收货) 3-已完成 4-已关闭 5-售后中</summary>
    public int Status { get; set; }

    /// <summary>该状态订单数量</summary>
    public int Count { get; set; }
}

/// <summary>售后情况统计</summary>
public class DashboardAfterSaleDto
{
    /// <summary>售后申请数（周期内订单产生的退款申请总数）</summary>
    public int ApplyCount { get; set; }

    /// <summary>同意数（审核通过 1 + 已退款 2）</summary>
    public int ApproveCount { get; set; }

    /// <summary>拒绝数（已驳回 3）</summary>
    public int RejectCount { get; set; }

    /// <summary>待审核数（待审核 0）</summary>
    public int PendingCount { get; set; }

    /// <summary>退款总金额（已退款 2 的金额合计）</summary>
    public decimal RefundAmount { get; set; }
}

/// <summary>库存预警单条（不受时间筛选影响）</summary>
public class DashboardInventoryWarningDto
{
    /// <summary>商品名称</summary>
    public string ProdName { get; set; } = string.Empty;

    /// <summary>SKU 名称（商品级预警时为「商品级」）</summary>
    public string SkuName { get; set; } = string.Empty;

    /// <summary>当前库存（SKU 级取该 SKU 库存；商品级取该商品全部 SKU 库存合计）</summary>
    public int CurrentStock { get; set; }

    /// <summary>预警阈值（库存 <= 此值时报警）</summary>
    public int WarningStock { get; set; }
}

/// <summary>看板请求参数（时间维度），所有受时间影响的接口共用</summary>
public class DashboardQueryDto
{
    /// <summary>
    /// 时间维度类型（可选）：day=近7天 / week=近8周 / month=近12个月。
    /// 传入 type 时由后端按本地时间自动计算起止；与 startDate/endDate 二选一，
    /// type 优先。两者都不传时默认 day（近7天）。
    /// </summary>
    public string? Type { get; set; }

    /// <summary>开始时间（本地日期，含当日 00:00），与 endDate 配套使用（自定义时间段）</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>结束时间（本地日期，含当日全天），与 startDate 配套使用</summary>
    public DateTime? EndDate { get; set; }
}
