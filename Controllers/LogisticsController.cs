using Jsd.Api.Models.Common;
using Jsd.Api.Models.Logistics;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 物流轨迹查询接口（物流轨迹查询模块，共 3 个 GET 接口）
///
/// 业务场景：客服在订单详情页点击「查看物流」时，根据订单中的快递单号调用本接口，
/// 实时获取并展示物流轨迹信息。
///
/// 权限标识见 <see cref="LogisticsPermissions"/>：
///   order:logistics:query（查询）/ order:logistics:refresh（刷新，前端按钮级）。
/// 后端仅做 JWT 鉴权（[Authorize]），按钮级权限由前端 v-permission 控制（与项目其它模块一致）。
/// </summary>
[ApiController]
[Route("api/logistics")]
[Authorize]
public class LogisticsController : ControllerBase
{
    private readonly ILogisticsService _logisticsService;

    public LogisticsController(ILogisticsService logisticsService)
    {
        _logisticsService = logisticsService;
    }

    /// <summary>智能识别快递公司（传入单号自动识别）</summary>
    /// <remarks>GET /api/logistics/identify?trackingNumber=xxx</remarks>
    [HttpGet("identify")]
    public async Task<ApiResponse<LogisticsIdentifyResult>> Identify([FromQuery] string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return ApiResponse<LogisticsIdentifyResult>.Fail("快递单号不能为空");
        }

        try
        {
            return await _logisticsService.IdentifyAsync(trackingNumber.Trim());
        }
        catch (Exception ex)
        {
            return ApiResponse<LogisticsIdentifyResult>.Fail($"识别失败：{ex.Message}");
        }
    }

    /// <summary>查询物流轨迹（命中缓存直接返回，否则调第三方）</summary>
    /// <remarks>GET /api/logistics/track?trackingNumber=xxx[&amp;carrierCode=shunfeng][&amp;orderId=123]</remarks>
    [HttpGet("track")]
    public async Task<ApiResponse<LogisticsTrackResult>> Track(
        [FromQuery] string trackingNumber,
        [FromQuery] string? carrierCode = null,
        [FromQuery] long? orderId = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return ApiResponse<LogisticsTrackResult>.Fail("快递单号不能为空");
        }

        try
        {
            return await _logisticsService.TrackAsync(trackingNumber.Trim(), carrierCode?.Trim(), orderId);
        }
        catch (Exception ex)
        {
            return ApiResponse<LogisticsTrackResult>.Fail($"查询失败：{ex.Message}");
        }
    }

    /// <summary>获取地图轨迹链接（可选能力）</summary>
    /// <remarks>GET /api/logistics/track/map?trackingNumber=xxx</remarks>
    [HttpGet("track/map")]
    public async Task<ApiResponse<LogisticsMapResult>> GetMap([FromQuery] string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return ApiResponse<LogisticsMapResult>.Fail("快递单号不能为空");
        }

        try
        {
            return await _logisticsService.GetMapAsync(trackingNumber.Trim());
        }
        catch (Exception ex)
        {
            return ApiResponse<LogisticsMapResult>.Fail($"获取失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 物流轨迹查询 权限标识常量（后端常量为准，与前端 v-permission、sys_menu.permission 三方一致）
/// </summary>
public static class LogisticsPermissions
{
    public const string Query = "order:logistics:query";
    public const string Refresh = "order:logistics:refresh";
}
