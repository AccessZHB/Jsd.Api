using Jsd.Api.Models.Common;
using Jsd.Api.Models.Logistics;

namespace Jsd.Api.Services;

/// <summary>
/// 物流轨迹查询服务接口
/// </summary>
public interface ILogisticsService
{
    /// <summary>智能识别快递公司（传入单号自动识别）</summary>
    Task<ApiResponse<LogisticsIdentifyResult>> IdentifyAsync(string trackingNumber);

    /// <summary>查询物流轨迹（命中缓存直接返回，否则调用第三方并落库日志）</summary>
    /// <param name="trackingNumber">快递单号</param>
    /// <param name="carrierCode">快递公司编码（可选；留空则先智能识别）</param>
    /// <param name="orderId">关联订单ID（可选；仅用于查询日志留痕）</param>
    Task<ApiResponse<LogisticsTrackResult>> TrackAsync(string trackingNumber, string? carrierCode = null, long? orderId = null);

    /// <summary>获取地图轨迹链接（可选能力）</summary>
    Task<ApiResponse<LogisticsMapResult>> GetMapAsync(string trackingNumber);
}
