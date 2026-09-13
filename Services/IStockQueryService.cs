using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockQuery;

namespace Jsd.Api.Services;

/// <summary>
/// 库存查询服务接口（纯只读）。
///
/// 接口清单（路由前缀 /api/stockQuery，与既有模块一致采用 camelCase）：
///   GET /api/stockQuery/overview  库存总览（5 项统计 + 最近 10 条变动）
///   GET /api/stockQuery/detail    库存明细（SKU 维度分页 + 预警状态标记）
///   GET /api/stockQuery/log       变动日志（类型/商品/日期 筛选分页）
///
/// 权限：stock_query:overview / stock_query:detail / stock_query:log
/// </summary>
public interface IStockQueryService
{
    /// <summary>库存总览：商品总数 / SKU总数 / 库存总量 / 预警数 / 缺货数 + 最近变动</summary>
    Task<ApiResponse<StockOverviewDto>> GetOverviewAsync();

    /// <summary>
    /// 库存明细：SKU 维度分页查询（多条件筛选 + 预警状态标记）
    /// </summary>
    /// <param name="keyword">关键词（商品名称/商品编码/SKU编码）</param>
    /// <param name="prodInfoId">商品ID</param>
    /// <param name="supplierId">供应商ID</param>
    /// <param name="stockStatus">库存状态：0-全部 1-正常 2-预警 3-缺货</param>
    Task<ApiResponse<PagedResult<StockDetailItemDto>>> GetDetailAsync(
        string? keyword, long? prodInfoId, long? supplierId, int stockStatus, int page, int pageSize);

    /// <summary>
    /// 变动日志：分页查询
    /// </summary>
    /// <param name="changeType">变动类型：1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他</param>
    /// <param name="prodInfoId">商品ID</param>
    /// <param name="keyword">关键词（操作人/备注）</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    Task<ApiResponse<PagedResult<StockLogItemDto>>> GetLogAsync(
        int? changeType, long? prodInfoId, string? keyword,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);
}
