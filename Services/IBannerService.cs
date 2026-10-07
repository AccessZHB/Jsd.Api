using Jsd.Api.Models.Common;
using Jsd.Api.Models.Home;

namespace Jsd.Api.Services;

/// <summary>
/// 首页轮播图（运营位）服务接口
/// </summary>
public interface IBannerService
{
    /// <summary>
    /// 获取「启用」状态的轮播图列表（按 sort 升序、同 sort 按 id 升序）。
    /// 小程序首页专用接口。
    /// </summary>
    Task<ApiResponse<List<BannerDto>>> GetEnabledListAsync();
}
