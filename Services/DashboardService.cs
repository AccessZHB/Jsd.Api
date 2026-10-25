using Jsd.Api.Entities;
using Jsd.Api.Models.Dashboard;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Jsd.Api.Services;

/// <summary>
/// 数据看板聚合服务。
///
/// 性能要点（务必沿用）：
/// 1. 所有金额/数量统计都在【数据库侧】用 GROUP BY + 聚合函数完成，不把订单全表拉到内存循环；
/// 2. 趋势按日期分组的 SQL 使用 MySQL 原生日期函数（DATE_FORMAT / DAYOFWEEK），
///    横坐标连续性由 C# 生成完整桶后再 left-join，缺数据的周期补 0，避免图表断线；
/// 3. 库存预警用一条 JOIN + 子查询聚合一次取出，最多取 100 条。
///
/// 口径约定（详见 DashboardDtos 注释）：
/// - "已支付" = order_status ∈ {1,2,3,5}（已支付/已发货/已完成/售后中），排除 待付款(0) 与 已关闭(4)；
/// - 销售额 = 已支付订单 pay_amount 之和 − 该时间段内订单产生的已退款(refund.status=2)金额；
/// - 时间维度：type 优先；自定义用 startDate/endDate；都不传默认 近7天。
/// </summary>
public class DashboardService
{
    private readonly AppDbContext _db;

    // 已支付订单状态集合（排除 待付款0、已关闭4）
    private static readonly int[] PaidStatuses = { 1, 2, 3, 5 };

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    #region 时间窗口解析

    /// <summary>
    /// 解析时间窗口与聚合粒度。
    /// 返回 (start, end, granularity)。end 为「次日 00:00」的排他边界，查询统一用 create_time &gt;= start &amp;&amp; &lt; end。
    /// </summary>
    public static (DateTime Start, DateTime End, string Granularity) ResolveWindow(
        string? type, DateTime? startDate, DateTime? endDate)
    {
        var today = DateTime.Today;

        if (!string.IsNullOrEmpty(type))
        {
            return type.ToLowerInvariant() switch
            {
                "week" => (MondayOf(today).AddDays(-7 * 7), today.AddDays(1), "week"),
                "month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-11), today.AddDays(1), "month"),
                _ => (today.AddDays(-6), today.AddDays(1), "day") // day / 其它 → 近7天
            };
        }

        if (startDate.HasValue && endDate.HasValue)
        {
            var start = startDate.Value.Date;
            var end = endDate.Value.Date.AddDays(1); // 含结束当天全天
            var days = (end - start).Days;
            var granularity = days <= 31 ? "day" : days <= 120 ? "week" : "month";
            return (start, end, granularity);
        }

        // 都不传：默认近7天
        return (today.AddDays(-6), today.AddDays(1), "day");
    }

    private static DateTime MondayOf(DateTime d)
    {
        var diff = (int)d.DayOfWeek - 1;
        if (diff < 0) diff += 7;
        return d.Date.AddDays(-diff);
    }

    #endregion

    #region 1. 核心概览

    public async Task<DashboardOverviewDto> GetOverviewAsync(DateTime start, DateTime end)
    {
        // 已支付订单（金额 + 数量）
        var paidOrders = _db.TrxOrders
            .Where(o => o.IsDeleted == 0 && PaidStatuses.Contains(o.OrderStatus)
                        && o.CreateTime >= start && o.CreateTime < end);

        var grossSales = await paidOrders.SumAsync(o => (decimal?)o.PayAmount) ?? 0m;
        var orderCount = await paidOrders.CountAsync();

        // 已退款金额 / 单数：退款单(status=2) 关联 时间段内创建的订单（已支付口径一致）
        var refundQuery = from r in _db.TrxRefunds
                         join o in _db.TrxOrders on r.OrderId equals o.Id
                         where o.IsDeleted == 0 && o.CreateTime >= start && o.CreateTime < end
                               && r.Status == 2
                         select r;

        var refundAmount = await refundQuery.SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;
        var refundCount = await refundQuery.CountAsync();

        var netSales = grossSales - refundAmount;
        var avg = orderCount > 0 ? netSales / orderCount : 0m;

        return new DashboardOverviewDto
        {
            SalesAmount = netSales,
            OrderCount = orderCount,
            AvgOrderValue = avg,
            RefundAmount = refundAmount,
            RefundCount = refundCount
        };
    }

    #endregion

    #region 2. 销售趋势

    public async Task<DashboardTrendDto> GetTrendAsync(DateTime start, DateTime end, string granularity)
    {
        var dateExpr = granularity switch
        {
            "week" => "DATE_FORMAT(DATE_SUB(o.create_time, INTERVAL (DAYOFWEEK(o.create_time)+5)%7 DAY),'%Y-%m-%d')",
            "month" => "DATE_FORMAT(o.create_time,'%Y-%m')",
            _ => "DATE_FORMAT(o.create_time,'%Y-%m-%d')"
        };

        // 销售额（已支付）
        var salesSql =
            "SELECT " + dateExpr + " AS DateKey, COALESCE(SUM(o.pay_amount),0) AS Sales, COUNT(o.id) AS OrderCount " +
            "FROM trx_order o " +
            "WHERE o.is_deleted=0 AND o.order_status IN (1,2,3,5) " +
            "AND o.create_time >= @start AND o.create_time < @end " +
            "GROUP BY DateKey";
        var salesRows = await _db.Database.SqlQueryRaw<BucketSales>(salesSql,
            new MySqlParameter("@start", start), new MySqlParameter("@end", end)).ToListAsync();

        // 已退款（按订单创建时间归属到周期）
        var refundSql =
            "SELECT " + dateExpr + " AS DateKey, COALESCE(SUM(r.refund_amount),0) AS Refund " +
            "FROM trx_refund r INNER JOIN trx_order o ON o.id = r.order_id " +
            "WHERE o.is_deleted=0 AND r.status=2 " +
            "AND o.create_time >= @start AND o.create_time < @end " +
            "GROUP BY DateKey";
        var refundRows = await _db.Database.SqlQueryRaw<BucketRefund>(refundSql,
            new MySqlParameter("@start", start), new MySqlParameter("@end", end)).ToListAsync();

        var refundByKey = refundRows.ToDictionary(r => r.DateKey, r => r.Refund);

        // 生成连续横坐标（含空桶补0）
        var buckets = GenerateBuckets(start, end, granularity);
        var points = buckets.Select(b => new DashboardTrendPointDto
        {
            DateLabel = b.Label,
            SalesAmount = (salesRows.FirstOrDefault(s => s.DateKey == b.Key)?.Sales ?? 0m)
                         - (refundByKey.TryGetValue(b.Key, out var rf) ? rf : 0m),
            OrderCount = salesRows.FirstOrDefault(s => s.DateKey == b.Key)?.OrderCount ?? 0
        }).ToList();

        return new DashboardTrendDto { Granularity = granularity, Points = points };
    }

    /// <summary>生成完整的连续桶（Key=数据库日期表达式格式，Label=展示用短标签）</summary>
    private static List<(string Key, string Label)> GenerateBuckets(DateTime start, DateTime end, string granularity)
    {
        var buckets = new List<(string, string)>();

        if (granularity == "week")
        {
            var monday = MondayOf(start);
            while (monday < end)
            {
                buckets.Add((monday.ToString("yyyy-MM-dd"), monday.ToString("MM/dd")));
                monday = monday.AddDays(7);
            }
        }
        else if (granularity == "month")
        {
            var m = new DateTime(start.Year, start.Month, 1);
            while (m < end)
            {
                buckets.Add((m.ToString("yyyy-MM"), m.ToString("yyyy-MM")));
                m = m.AddMonths(1);
            }
        }
        else // day
        {
            for (var d = start.Date; d < end; d = d.AddDays(1))
            {
                buckets.Add((d.ToString("yyyy-MM-dd"), d.ToString("MM/dd")));
            }
        }

        return buckets;
    }

    #endregion

    #region 3. 商品销量排行 Top10

    public async Task<List<DashboardTopProductDto>> GetTopProductsAsync(DateTime start, DateTime end)
    {
        const string sql =
            "SELECT oi.prod_info_id AS ProdInfoId, MAX(oi.prod_name) AS ProdName, " +
            "COALESCE(SUM(oi.quantity),0) AS Quantity, COALESCE(SUM(oi.subtotal_amount),0) AS Sales " +
            "FROM trx_order_item oi INNER JOIN trx_order o ON o.id = oi.order_id " +
            "WHERE o.is_deleted=0 AND o.order_status IN (1,2,3,5) " +
            "AND o.create_time >= @start AND o.create_time < @end " +
            "GROUP BY oi.prod_info_id ORDER BY Quantity DESC LIMIT 10";

        var rows = await _db.Database.SqlQueryRaw<TopProductRow>(sql,
            new MySqlParameter("@start", start), new MySqlParameter("@end", end)).ToListAsync();

        return rows.Select(r => new DashboardTopProductDto
        {
            ProdInfoId = r.ProdInfoId,
            ProdName = r.ProdName,
            Quantity = (int)r.Quantity,
            SalesAmount = r.Sales
        }).ToList();
    }

    #endregion

    #region 4. 订单状态分布

    public async Task<List<DashboardOrderStatusDto>> GetOrderStatusAsync(DateTime start, DateTime end)
    {
        // 全部 6 个状态（0-5），未出现的补 0，保证饼图完整
        var existing = await _db.TrxOrders
            .Where(o => o.IsDeleted == 0 && o.CreateTime >= start && o.CreateTime < end)
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var map = existing.ToDictionary(x => x.Status, x => x.Count);
        var result = new List<DashboardOrderStatusDto>();
        for (var status = 0; status <= 5; status++)
        {
            result.Add(new DashboardOrderStatusDto { Status = status, Count = map.GetValueOrDefault(status, 0) });
        }
        return result;
    }

    #endregion

    #region 5. 售后情况统计

    public async Task<DashboardAfterSaleDto> GetAfterSalesAsync(DateTime start, DateTime end)
    {
        var q = from r in _db.TrxRefunds
                join o in _db.TrxOrders on r.OrderId equals o.Id
                where o.IsDeleted == 0 && o.CreateTime >= start && o.CreateTime < end
                select r;

        var applyCount = await q.CountAsync();
        var approveCount = await q.CountAsync(r => r.Status == 1 || r.Status == 2);
        var rejectCount = await q.CountAsync(r => r.Status == 3);
        var pendingCount = await q.CountAsync(r => r.Status == 0);
        var refundAmount = await q.Where(r => r.Status == 2).SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;

        return new DashboardAfterSaleDto
        {
            ApplyCount = applyCount,
            ApproveCount = approveCount,
            RejectCount = rejectCount,
            PendingCount = pendingCount,
            RefundAmount = refundAmount
        };
    }

    #endregion

    #region 6. 库存预警（不受时间筛选影响）

    public async Task<List<DashboardInventoryWarningDto>> GetInventoryWarningsAsync()
    {
        const string sql =
            "SELECT p.prod_info_name AS ProdName, " +
            "CASE WHEN w.sku_id IS NULL THEN '商品级' ELSE COALESCE(sku.sku_name,'') END AS SkuName, " +
            "COALESCE(sku.stock, agg.skustock, 0) AS CurrentStock, " +
            "w.warning_stock AS WarningStock " +
            "FROM log_stock_warning w " +
            "INNER JOIN prod_info p ON p.id = w.prod_info_id " +
            "LEFT JOIN prod_sku sku ON sku.id = w.sku_id " +
            "LEFT JOIN (SELECT prod_info_id, COALESCE(SUM(stock),0) AS skustock FROM prod_sku GROUP BY prod_info_id) agg " +
            "  ON agg.prod_info_id = w.prod_info_id " +
            "WHERE w.status = 1 " +
            "AND COALESCE(sku.stock, agg.skustock, 0) <= w.warning_stock " +
            "ORDER BY CurrentStock ASC LIMIT 100";

        var rows = await _db.Database.SqlQueryRaw<InventoryRow>(sql).ToListAsync();

        return rows.Select(r => new DashboardInventoryWarningDto
        {
            ProdName = r.ProdName,
            SkuName = r.SkuName,
            CurrentStock = (int)r.CurrentStock,
            WarningStock = r.WarningStock
        }).ToList();
    }

    #endregion

    #region 内部映射类（数据库原生聚合结果，不进 EF 模型）

    private class BucketSales
    {
        public string DateKey { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public int OrderCount { get; set; }
    }

    private class BucketRefund
    {
        public string DateKey { get; set; } = string.Empty;
        public decimal Refund { get; set; }
    }

    private class TopProductRow
    {
        public long ProdInfoId { get; set; }
        public string ProdName { get; set; } = string.Empty;
        public long Quantity { get; set; }
        public decimal Sales { get; set; }
    }

    private class InventoryRow
    {
        public string ProdName { get; set; } = string.Empty;
        public string SkuName { get; set; } = string.Empty;
        public long CurrentStock { get; set; }
        public int WarningStock { get; set; }
    }

    #endregion
}
