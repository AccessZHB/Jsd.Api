using Jsd.Api.Models.Common;
using Jsd.Api.Models.Home;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 首页轮播图（运营位）接口
/// 数据来源：mkt_banner 表（后台维护），小程序首页读取启用中的轮播。
/// </summary>
[ApiController]
[Route("api/banner")]
public class BannerController : ControllerBase
{
    private readonly IBannerService _bannerService;

    public BannerController(IBannerService bannerService)
    {
        _bannerService = bannerService;
    }

    /// <summary>
    /// 轮播图列表（小程序首页用）
    /// 示例：GET /api/banner/list
    /// 返回：{ id, title, imageUrl, path, sort }，按 sort 升序；
    /// path 为小程序内部页面路径，为空表示点击不跳转。
    /// 轮播属公开运营内容，无需登录即可访问。
    /// </summary>
    [HttpGet("list")]
    [AllowAnonymous]
    public async Task<ApiResponse<List<BannerDto>>> GetList()
    {
        return await _bannerService.GetEnabledListAsync();
    }
}
