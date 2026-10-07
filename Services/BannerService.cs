using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Home;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 首页轮播图（运营位）服务实现。
///
/// 数据来自 mkt_banner 表，后台维护；小程序端只读取 status=1 的记录。
/// 直接用泛型仓储 IRepository&lt;MktBanner&gt;，无需为单表查询单独建仓储。
/// </summary>
public class BannerService : IBannerService
{
    private readonly IRepository<MktBanner> _repository;

    public BannerService(IRepository<MktBanner> repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<List<BannerDto>>> GetEnabledListAsync()
    {
        // 只取启用的轮播图
        var list = await _repository.GetListAsync(b => b.Status == 1);

        var dtos = list
            .OrderBy(b => b.Sort)
            .ThenBy(b => b.Id)
            .Select(b => new BannerDto
            {
                Id = b.Id,
                Title = b.Title,
                ImageUrl = b.ImageUrl,
                Path = b.Path,
                Sort = b.Sort
            })
            .ToList();

        return ApiResponse<List<BannerDto>>.Success(dtos);
    }
}
