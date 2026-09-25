using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 订单价格快照接口（价格策略与会员余额模块，内部接口）
/// 路由前缀：/api/order-price-snapshots
///
/// 价格快照是历史价格追溯与毛利分析的【唯一依据】：
/// 下单瞬间冻结「标准售价 → 命中策略 → 命中规则 → 最终成交价 → 折扣详情」，
/// 之后商品改价、策略停用、等级调整都不允许影响历史订单。
/// </summary>
[ApiController]
[Route("api/order-price-snapshots")]
[Authorize]
public class OrderPriceSnapshotController : ControllerBase
{
    private readonly IPriceService _priceService;

    public OrderPriceSnapshotController(IPriceService priceService)
    {
        _priceService = priceService;
    }

    /// <summary>分页查询订单价格快照（订单号 / 客户 / 商品 / 命中策略类型 / 时间范围）</summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<OrderPriceSnapshotDto>>> GetPage(
        [FromQuery] OrderPriceSnapshotQueryDto query)
    {
        return await _priceService.GetSnapshotsPagedAsync(query);
    }

    /// <summary>内部接口：下单时批量写入价格快照</summary>
    [HttpPost("batch")]
    public async Task<ApiResponse<bool>> SaveBatch([FromBody] OrderPriceSnapshotBatchDto dto)
    {
        return await _priceService.SaveSnapshotsAsync(dto);
    }

    /// <summary>按订单查询价格快照（历史价格追溯）</summary>
    [HttpGet("order/{orderId:long}")]
    public async Task<ApiResponse<List<OrderPriceSnapshotDto>>> GetByOrder(long orderId)
    {
        return await _priceService.GetSnapshotsAsync(orderId);
    }
}
