using Jsd.Api.Entities;
using Jsd.Api.Models.Cms;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Controllers;

/// <summary>
/// 内容管理 - 公告接口
/// 路由前缀：/api/cms/announcement
///
/// 接口清单：
///   公开（小程序首页）：GET  /api/cms/announcement/latest   取已发布中的最新 / 置顶一条
///   后台专用（需登录且为管理员令牌，会员令牌拒绝）：
///     GET    /api/cms/announcement/list          分页列表
///     POST   /api/cms/announcement/add           新增
///     PUT    /api/cms/announcement/edit          修改
///     DELETE /api/cms/announcement/{id}          删除
///     PUT    /api/cms/announcement/update-status 切换发布/草稿、设置置顶
/// </summary>
[ApiController]
[Route("api/cms/announcement")]
public class CmsAnnouncementController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _currentUser;

    public CmsAnnouncementController(AppDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ============================================================
    // 0. 公开：小程序首页取最新 / 置顶公告
    // ============================================================

    /// <summary>
    /// 取已发布(status=1)公告中的最新 / 置顶一条，供小程序首页公告栏滚动展示。
    /// 排序规则：先按 is_top 降序（置顶优先），再按 update_time 降序（最新优先）。
    /// 示例：GET /api/cms/announcement/latest
    /// 返回：{ title, content }
    /// </summary>
    [HttpGet("latest")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementLatestDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<AnnouncementLatestDto>> GetLatest()
    {
        var entity = await _db.CmsAnnouncements.AsNoTracking()
            .Where(a => a.Status == 1)
            .OrderByDescending(a => a.IsTop)          // 置顶优先
            .ThenByDescending(a => a.UpdateTime)       // 其次最新
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return ApiResponse<AnnouncementLatestDto>.Success(
                new AnnouncementLatestDto(), "暂无公告");
        }

        return ApiResponse<AnnouncementLatestDto>.Success(new AnnouncementLatestDto
        {
            Title = entity.Title,
            Content = entity.Content ?? string.Empty
        });
    }

    // ============================================================
    // 1. 后台：分页列表
    // ============================================================

    /// <summary>
    /// 后台公告分页列表（按置顶优先 + 更新时间倒序）。
    /// 示例：GET /api/cms/announcement/list?page=1&amp;pageSize=10
    /// </summary>
    [HttpGet("list")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AnnouncementListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<AnnouncementListDto>>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<PagedResult<AnnouncementListDto>>.Fail("无权限访问后台接口", 403);
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.CmsAnnouncements.AsNoTracking();
        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(a => a.IsTop)
            .ThenByDescending(a => a.UpdateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AnnouncementListDto
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                IsTop = a.IsTop,
                Status = a.Status,
                UpdateTime = a.UpdateTime
            })
            .ToListAsync();

        return ApiResponse<PagedResult<AnnouncementListDto>>.Success(
            PagedResult<AnnouncementListDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 2. 后台：新增
    // ============================================================

    /// <summary>
    /// 新增公告。
    /// 示例：POST /api/cms/announcement/add
    ///   body: { "title": "春节休整通知", "content": "…", "isTop": 0, "status": 1 }
    /// </summary>
    [HttpPost("add")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<long>> Add([FromBody] AnnouncementSaveDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<long>.Fail("无权限访问后台接口", 403);
        }

        var entity = new CmsAnnouncement
        {
            Title = dto.Title.Trim(),
            Content = dto.Content,
            IsTop = dto.IsTop == 1 ? 1 : 0,
            Status = dto.Status == 1 ? 1 : 0,
            CreateTime = DateTime.Now
        };

        await _db.CmsAnnouncements.AddAsync(entity);
        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "公告已保存");
    }

    // ============================================================
    // 3. 后台：修改
    // ============================================================

    /// <summary>
    /// 修改公告（Id&gt;0）。
    /// 示例：PUT /api/cms/announcement/edit
    ///   body: { "id": 12, "title": "…", "content": "…", "isTop": 1, "status": 1 }
    /// </summary>
    [HttpPut("edit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<long>> Edit([FromBody] AnnouncementSaveDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<long>.Fail("无权限访问后台接口", 403);
        }

        if (dto.Id <= 0)
        {
            return ApiResponse<long>.Fail("公告ID非法");
        }

        var entity = await _db.CmsAnnouncements.FirstOrDefaultAsync(a => a.Id == dto.Id);
        if (entity == null)
        {
            return ApiResponse<long>.Fail("公告不存在", 404);
        }

        entity.Title = dto.Title.Trim();
        entity.Content = dto.Content;
        entity.IsTop = dto.IsTop == 1 ? 1 : 0;
        entity.Status = dto.Status == 1 ? 1 : 0;

        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "公告已更新");
    }

    // ============================================================
    // 4. 后台：删除
    // ============================================================

    /// <summary>
    /// 删除公告（物理删除；cms_announcement 无外键依赖）。
    /// 示例：DELETE /api/cms/announcement/12
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<bool>> Delete(long id)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<bool>.Fail("无权限访问后台接口", 403);
        }

        var entity = await _db.CmsAnnouncements.FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("公告不存在", 404);
        }

        _db.CmsAnnouncements.Remove(entity);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "公告已删除");
    }

    // ============================================================
    // 5. 后台：切换发布 / 草稿、设置置顶
    // ============================================================

    /// <summary>
    /// 切换发布/草稿状态、设置置顶（status / isTop 不传则保持原值）。
    /// 示例：PUT /api/cms/announcement/update-status
    ///   body: { "id": 12, "status": 1, "isTop": 1 }
    /// </summary>
    [HttpPut("update-status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<bool>> UpdateStatus([FromBody] AnnouncementStatusDto dto)
    {
        if (_currentUser.IsMember)
        {
            return ApiResponse<bool>.Fail("无权限访问后台接口", 403);
        }

        var entity = await _db.CmsAnnouncements.FirstOrDefaultAsync(a => a.Id == dto.Id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("公告不存在", 404);
        }

        if (dto.Status.HasValue) entity.Status = dto.Status.Value == 1 ? 1 : 0;
        if (dto.IsTop.HasValue) entity.IsTop = dto.IsTop.Value == 1 ? 1 : 0;

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "公告状态已更新");
    }
}
