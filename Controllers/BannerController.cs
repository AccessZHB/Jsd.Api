using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Home;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Controllers;

/// <summary>
/// 首页轮播图（运营位）接口
/// 数据来源：mkt_banner 表（后台维护），小程序首页读取启用中的轮播。
///
/// 接口清单：
///   公开（小程序首页）：GET  /api/banner/list            取启用中的轮播（按 sort 升序）
///   后台专用（需登录且为管理员令牌，会员令牌拒绝）：
///     GET    /api/banner/admin/list          分页列表（全部状态）
///     POST   /api/banner/admin/add           新增
///     PUT    /api/banner/admin/edit          修改
///     DELETE /api/banner/admin/{id}          删除
///     PUT    /api/banner/admin/update-status 切换启用 / 禁用
/// </summary>
[ApiController]
[Route("api/banner")]
public class BannerController : ControllerBase
{
    private readonly IBannerService _bannerService;
    private readonly AppDbContext _db;
    private readonly CurrentUserService _currentUser;

    public BannerController(
        IBannerService bannerService,
        AppDbContext db,
        CurrentUserService currentUser)
    {
        _bannerService = bannerService;
        _db = db;
        _currentUser = currentUser;
    }

    // ============================================================
    // 0. 公开：小程序首页取启用中的轮播
    // ============================================================

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
        var resp = await _bannerService.GetEnabledListAsync();
        var list = resp.Data ?? new List<BannerDto>();

        // 小程序 <image> 无法加载相对路径，必须把 /upload/... 拼成完整 URL
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        foreach (var dto in list)
        {
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl)
                && dto.ImageUrl.StartsWith('/'))
            {
                dto.ImageUrl = baseUrl + dto.ImageUrl;
            }
        }

        return resp;
    }

    // ============================================================
    // 1. 后台：分页列表（全部状态，按 sort 升序 + id 升序）
    // ============================================================

    /// <summary>
    /// 后台轮播图分页列表（启用 / 禁用全部返回，按 sort 升序、同 sort 按 id 升序）。
    /// 示例：GET /api/banner/admin/list?page=1&amp;pageSize=10
    /// </summary>
    [HttpGet("admin/list")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BannerListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<BannerListDto>>> GetAdminList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<PagedResult<BannerListDto>>.Fail("无权限访问后台接口", 403);
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.MktBanners.AsNoTracking();
        var total = await query.CountAsync();

        var list = await query
            .OrderBy(b => b.Sort)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BannerListDto
            {
                Id = b.Id,
                Title = b.Title,
                ImageUrl = b.ImageUrl,
                Path = b.Path,
                Sort = b.Sort,
                Status = b.Status,
                CreateTime = b.CreateTime,
                UpdateTime = b.UpdateTime
            })
            .ToListAsync();

        return ApiResponse<PagedResult<BannerListDto>>.Success(
            PagedResult<BannerListDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 2. 后台：新增
    // ============================================================

    /// <summary>
    /// 新增轮播图。
    /// 示例：POST /api/banner/admin/add
    ///   body: { "title": "首页大促", "imageUrl": "/upload/202610/xxx.jpg", "path": "/pages/category/list", "sort": 1, "status": 1 }
    /// 说明：imageUrl 由前端上传 /api/file/upload 后回填的相对地址（或完整 http(s) 地址）。
    /// </summary>
    [HttpPost("admin/add")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<long>> AdminAdd([FromBody] BannerSaveDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<long>.Fail("无权限访问后台接口", 403);
        }

        var entity = new MktBanner
        {
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
            ImageUrl = dto.ImageUrl.Trim(),
            Path = string.IsNullOrWhiteSpace(dto.Path) ? null : dto.Path.Trim(),
            Sort = dto.Sort,
            Status = dto.Status == 1 ? 1 : 0
        };

        await _db.MktBanners.AddAsync(entity);
        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "轮播已添加");
    }

    // ============================================================
    // 3. 后台：修改
    // ============================================================

    /// <summary>
    /// 修改轮播图（Id&gt;0）。
    /// 示例：PUT /api/banner/admin/edit
    ///   body: { "id": 5, "title": "…", "imageUrl": "…", "path": "…", "sort": 2, "status": 1 }
    /// </summary>
    [HttpPut("admin/edit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<long>> AdminEdit([FromBody] BannerSaveDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<long>.Fail("无权限访问后台接口", 403);
        }

        if (dto.Id <= 0)
        {
            return ApiResponse<long>.Fail("轮播ID非法");
        }

        var entity = await _db.MktBanners.FirstOrDefaultAsync(b => b.Id == dto.Id);
        if (entity == null)
        {
            return ApiResponse<long>.Fail("轮播不存在", 404);
        }

        entity.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        entity.ImageUrl = dto.ImageUrl.Trim();
        entity.Path = string.IsNullOrWhiteSpace(dto.Path) ? null : dto.Path.Trim();
        entity.Sort = dto.Sort;
        entity.Status = dto.Status == 1 ? 1 : 0;

        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "轮播已更新");
    }

    // ============================================================
    // 4. 后台：删除
    // ============================================================

    /// <summary>
    /// 删除轮播图（物理删除；mkt_banner 无外键依赖）。
    /// 示例：DELETE /api/banner/admin/5
    /// </summary>
    [HttpDelete("admin/{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<bool>> AdminDelete(long id)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<bool>.Fail("无权限访问后台接口", 403);
        }

        var entity = await _db.MktBanners.FirstOrDefaultAsync(b => b.Id == id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("轮播不存在", 404);
        }

        _db.MktBanners.Remove(entity);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "轮播已删除");
    }

    // ============================================================
    // 5. 后台：切换启用 / 禁用
    // ============================================================

    /// <summary>
    /// 切换轮播启用 / 禁用状态。
    /// 示例：PUT /api/banner/admin/update-status
    ///   body: { "id": 5, "status": 0 }   // 0-禁用 1-启用
    /// </summary>
    [HttpPut("admin/update-status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<bool>> AdminUpdateStatus([FromBody] BannerStatusDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<bool>.Fail("无权限访问后台接口", 403);
        }

        var entity = await _db.MktBanners.FirstOrDefaultAsync(b => b.Id == dto.Id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("轮播不存在", 404);
        }

        entity.Status = dto.Status == 1 ? 1 : 0;

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "轮播状态已更新");
    }
}
