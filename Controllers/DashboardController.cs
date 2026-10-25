using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dashboard;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 数据看板（经营分析）
/// 路由前缀：/api/admin/dashboard
///
/// 权限：沿用项目现有鉴权（[Authorize] 要求有效 JWT）；
/// 额外拦截会员令牌（user_type=member），避免小程序会员越权访问后台经营数据。
///
/// 接口清单：
///   GET /api/admin/dashboard/overview      核心概览（销售额/订单数/客单价/退款）
///   GET /api/admin/dashboard/trend         销售趋势（按 day/week/month 聚合，横坐标连续补0）
///   GET /api/admin/dashboard/top-products  商品销量排行 Top10
///   GET /api/admin/dashboard/order-status  订单状态分布
///   GET /api/admin/dashboard/after-sales   售后情况统计
///   GET /api/admin/dashboard/inventory-warning  库存预警（不受时间筛选影响）
///
/// 时间参数：type(day/week/month) 优先；否则用 startDate/endDate（自定义）；都不传默认 近7天。
/// 库存预警接口不走时间筛选（预警与时间无关）。
///
/// 返回约定：与项目其它接口一致，成功/业务失败一律返回 ApiResponse&lt;T&gt; 信封，
/// 业务码置于 Code（403 表示会员越权），HTTP 状态码固定 200，由前端按 code===200 判定。
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly CurrentUserService _currentUser;

    public DashboardController(DashboardService dashboardService, CurrentUserService currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    /// <summary>仅管理员可访问：会员令牌直接拒绝。返回非 null 表示应直接返回该错误响应。</summary>
    private ApiResponse<T>? ForbidMember<T>()
    {
        if (_currentUser.IsMember)
            return ApiResponse<T>.Fail("无权限访问后台数据", 403);
        return null;
    }

    /// <summary>1. 核心概览卡片</summary>
    [HttpGet("overview")]
    public async Task<ApiResponse<DashboardOverviewDto>> Overview(
        [FromQuery] string? type, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var forbid = ForbidMember<DashboardOverviewDto>();
        if (forbid != null) return forbid;

        var (start, end, _) = DashboardService.ResolveWindow(type, startDate, endDate);
        var data = await _dashboardService.GetOverviewAsync(start, end);
        return ApiResponse<DashboardOverviewDto>.Success(data);
    }

    /// <summary>2. 销售趋势（折线图）</summary>
    [HttpGet("trend")]
    public async Task<ApiResponse<DashboardTrendDto>> Trend(
        [FromQuery] string? type, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var forbid = ForbidMember<DashboardTrendDto>();
        if (forbid != null) return forbid;

        var (start, end, granularity) = DashboardService.ResolveWindow(type, startDate, endDate);
        var data = await _dashboardService.GetTrendAsync(start, end, granularity);
        return ApiResponse<DashboardTrendDto>.Success(data);
    }

    /// <summary>3. 商品销量排行 Top10</summary>
    [HttpGet("top-products")]
    public async Task<ApiResponse<List<DashboardTopProductDto>>> TopProducts(
        [FromQuery] string? type, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var forbid = ForbidMember<List<DashboardTopProductDto>>();
        if (forbid != null) return forbid;

        var (start, end, _) = DashboardService.ResolveWindow(type, startDate, endDate);
        var data = await _dashboardService.GetTopProductsAsync(start, end);
        return ApiResponse<List<DashboardTopProductDto>>.Success(data);
    }

    /// <summary>4. 订单状态分布</summary>
    [HttpGet("order-status")]
    public async Task<ApiResponse<List<DashboardOrderStatusDto>>> OrderStatus(
        [FromQuery] string? type, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var forbid = ForbidMember<List<DashboardOrderStatusDto>>();
        if (forbid != null) return forbid;

        var (start, end, _) = DashboardService.ResolveWindow(type, startDate, endDate);
        var data = await _dashboardService.GetOrderStatusAsync(start, end);
        return ApiResponse<List<DashboardOrderStatusDto>>.Success(data);
    }

    /// <summary>5. 售后情况统计</summary>
    [HttpGet("after-sales")]
    public async Task<ApiResponse<DashboardAfterSaleDto>> AfterSales(
        [FromQuery] string? type, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var forbid = ForbidMember<DashboardAfterSaleDto>();
        if (forbid != null) return forbid;

        var (start, end, _) = DashboardService.ResolveWindow(type, startDate, endDate);
        var data = await _dashboardService.GetAfterSalesAsync(start, end);
        return ApiResponse<DashboardAfterSaleDto>.Success(data);
    }

    /// <summary>6. 库存预警列表（不受时间筛选影响，单独获取）</summary>
    [HttpGet("inventory-warning")]
    public async Task<ApiResponse<List<DashboardInventoryWarningDto>>> InventoryWarning()
    {
        var forbid = ForbidMember<List<DashboardInventoryWarningDto>>();
        if (forbid != null) return forbid;

        var data = await _dashboardService.GetInventoryWarningsAsync();
        return ApiResponse<List<DashboardInventoryWarningDto>>.Success(data);
    }
}
