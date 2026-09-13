using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockQuery;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 库存查询接口（纯只读）
/// 路由前缀：/api/stockQuery（与 stockIn / stockOut / stockCheck / stockWarning 一致采用 camelCase）
/// 权限标识见 <see cref="StockQueryPermissions"/>（stock_query:overview / detail / log）
///
/// 接口清单：
///   1. GET /api/stockQuery/overview  库存总览（5 项统计 + 最近 10 条变动）
///   2. GET /api/stockQuery/detail    库存明细（SKU 维度分页 + 预警状态标记）
///   3. GET /api/stockQuery/log       变动日志（类型/商品/日期 筛选分页）
///
/// 涉及的 5 张表均为已有表（log_stock_log、prod_sku、prod_info、sup_supplier、log_stock_warning），
/// 本模块只做关联查询，不新增实体、不写库。
/// </summary>
[ApiController]
[Route("api/stockQuery")]
[Authorize]  // 必须登录
public class StockQueryController : ControllerBase
{
    private readonly IStockQueryService _stockQueryService;

    public StockQueryController(IStockQueryService stockQueryService)
    {
        _stockQueryService = stockQueryService;
    }

    /// <summary>
    /// 1. 库存总览（权限：stock_query:overview）
    /// 返回：商品总数 / SKU总数 / 库存总量 / 预警数 / 缺货数 + 最近 10 条变动记录。
    /// 示例：GET /api/stockQuery/overview
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<StockOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<StockOverviewDto>> GetOverview()
    {
        return await _stockQueryService.GetOverviewAsync();
    }

    /// <summary>
    /// 2. 库存明细（权限：stock_query:detail）
    /// SKU 维度分页查询，支持关键词 / 商品 / 供应商 / 库存状态筛选，带预警状态标记。
    /// 示例：GET /api/stockQuery/detail?keyword=镜片&amp;stockStatus=2&amp;page=1&amp;pageSize=10
    /// </summary>
    /// <param name="keyword">关键词：商品名称 / 商品编码 / SKU编码（可空）</param>
    /// <param name="prodInfoId">商品ID（可空）</param>
    /// <param name="supplierId">供应商ID（可空）</param>
    /// <param name="stockStatus">库存状态：0-全部 1-正常 2-预警 3-缺货（默认 0）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("detail")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockDetailItemDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockDetailItemDto>>> GetDetail(
        [FromQuery] string? keyword,
        [FromQuery] long? prodInfoId,
        [FromQuery] long? supplierId,
        [FromQuery] int stockStatus = 0,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockQueryService.GetDetailAsync(
            keyword, prodInfoId, supplierId, stockStatus, page, pageSize);
    }

    /// <summary>
    /// 3. 变动日志（权限：stock_query:log）
    /// 支持变动类型 / 商品 / 关键词 / 日期范围 筛选。
    /// 示例：GET /api/stockQuery/log?changeType=1&amp;startTime=2026-09-01&amp;endTime=2026-09-12&amp;page=1
    /// </summary>
    /// <param name="changeType">变动类型：1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他（可空）</param>
    /// <param name="prodInfoId">商品ID（可空）</param>
    /// <param name="keyword">关键词：操作人 / 备注（可空）</param>
    /// <param name="startTime">开始时间（可空）</param>
    /// <param name="endTime">结束时间（可空，只传日期按当天结束处理）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("log")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockLogItemDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockLogItemDto>>> GetLog(
        [FromQuery] int? changeType,
        [FromQuery] long? prodInfoId,
        [FromQuery] string? keyword,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockQueryService.GetLogAsync(
            changeType, prodInfoId, keyword, startTime, endTime, page, pageSize);
    }
}
