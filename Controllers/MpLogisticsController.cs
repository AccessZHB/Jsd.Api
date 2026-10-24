using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Logistics;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Controllers;

/// <summary>
/// 会员端物流轨迹接口（小程序「查看物流」兜底页数据源）。
/// 路由前缀：/api/mp/logistics
///
/// 与后台 /api/logistics/track 的区别：
///   1. 【无权限码校验】后台接口带 [Permission(LogisticsPermissions.Query)]（order:logistics:query），
///      会员令牌会被 403；本控制器只要求登录，且必须是【会员令牌】。
///   2. 【强制归属校验】只接受 orderId，单号一律由后端按订单查出（不信任前端传参），
///      非本人订单按「订单不存在」返回，防止越权查询他人物流。
///   3. 【不传承运商编码】trx_order.ship_company 存的是中文名（如「顺丰速运」），
///      而第三方（快递100）要的是编码（shunfeng），故这里传 null 交由服务层智能识别，
///      避免把中文名当编码传过去导致查询失败。
/// </summary>
[ApiController]
[Route("api/mp/logistics")]
[Authorize]
public class MpLogisticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogisticsService _logisticsService;
    private readonly CurrentUserService _currentUser;

    public MpLogisticsController(
        AppDbContext db,
        ILogisticsService logisticsService,
        CurrentUserService currentUser)
    {
        _db = db;
        _logisticsService = logisticsService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 按订单查询物流轨迹（会员只能查自己的订单）。
    /// 示例：GET /api/mp/logistics/track?orderId=123
    /// </summary>
    [HttpGet("track")]
    [ProducesResponseType(typeof(ApiResponse<MpLogisticsTrackResult>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<MpLogisticsTrackResult>> Track([FromQuery] long orderId)
    {
        // 必须是会员令牌：管理员令牌不含 member_id（与 /api/order/my 同策略）
        if (!_currentUser.IsMember || _currentUser.MemberId <= 0)
        {
            return ApiResponse<MpLogisticsTrackResult>.Fail("请使用会员身份登录", 401);
        }

        if (orderId <= 0)
        {
            return ApiResponse<MpLogisticsTrackResult>.Fail("订单ID不能为空");
        }

        var order = await _db.TrxOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.IsDeleted == 0);

        // 越权防护：订单不存在或不属于当前会员，统一按「订单不存在」返回，避免泄露他人信息
        if (order == null || order.BuyerId != _currentUser.MemberId)
        {
            return ApiResponse<MpLogisticsTrackResult>.Fail("订单不存在", 404);
        }

        var shipNo = (order.ShipNo ?? string.Empty).Trim();
        if (shipNo.Length == 0)
        {
            return ApiResponse<MpLogisticsTrackResult>.Fail("该订单暂未填写物流单号");
        }

        // carrierCode 传 null：由服务层智能识别（ship_company 是中文名，不能直接当编码传）
        var res = await _logisticsService.TrackAsync(shipNo, null, order.Id);
        if (res.Code != 200 || res.Data == null)
        {
            return ApiResponse<MpLogisticsTrackResult>.Fail(res.Message ?? "物流查询失败");
        }

        var t = res.Data;
        return ApiResponse<MpLogisticsTrackResult>.Success(new MpLogisticsTrackResult
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            ShipCompany = order.ShipCompany,
            ShipNo = shipNo,
            ShippingImageUrl = order.ShippingImageUrl ?? string.Empty,
            TrackingNumber = t.TrackingNumber,
            CarrierName = t.CarrierName,
            State = t.State,
            StateText = t.StateText,
            Nodes = t.Nodes,
            EstimatedDelivery = t.EstimatedDelivery,
            IsMock = t.IsMock,
            QueryTime = t.QueryTime
        });
    }
}

/// <summary>
/// 会员端物流轨迹返回体：订单信息 + 第三方轨迹。
/// 附带订单号与公司名，便于兜底页在第三方查不到时仍能展示基础数据。
/// </summary>
public class MpLogisticsTrackResult
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>后台发货时录入的物流公司（中文名）</summary>
    public string ShipCompany { get; set; } = string.Empty;

    /// <summary>后台发货时录入的物流单号</summary>
    public string ShipNo { get; set; } = string.Empty;

    /// <summary>物流面单照片路径（发货时上传的面单/条形码照片，相对路径；可能为空）</summary>
    public string ShippingImageUrl { get; set; } = string.Empty;

    /// <summary>第三方回传的单号（一般与 ShipNo 一致）</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>识别出的快递公司名</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>物流状态：0在途 1揽收 2疑难 3签收 4退签 5派件 6退回</summary>
    public int State { get; set; }

    public string StateText { get; set; } = string.Empty;

    /// <summary>轨迹节点（最新在最前）</summary>
    public List<LogisticsTrackNode> Nodes { get; set; } = new();

    public string? EstimatedDelivery { get; set; }

    /// <summary>true 表示未配置第三方密钥返回的演示数据，前端需提示</summary>
    public bool IsMock { get; set; }

    public string QueryTime { get; set; } = string.Empty;
}
