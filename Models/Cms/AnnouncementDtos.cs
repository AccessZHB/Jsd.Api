using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Cms;

/// <summary>
/// 小程序首页公告栏返回（仅取已发布中的最新 / 置顶一条的标题与内容）
/// </summary>
public class AnnouncementLatestDto
{
    /// <summary>公告标题（无公告时为空串，前端回退到默认提示）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>公告内容（富文本 HTML 原文；小程序端剥离 HTML 标签后滚动展示）</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 公告列表项（后台管理分页列表）
/// </summary>
public class AnnouncementListDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    /// <summary>是否置顶：1-置顶 0-不置顶</summary>
    public int IsTop { get; set; }
    /// <summary>状态：1-发布 0-草稿</summary>
    public int Status { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 新增 / 修改公告入参（Id=0 新增，&gt;0 修改）
/// </summary>
public class AnnouncementSaveDto
{
    /// <summary>公告ID（新增时传 0）</summary>
    public long Id { get; set; }

    /// <summary>公告标题（必填，≤200 字）</summary>
    [Required(ErrorMessage = "公告标题不能为空")]
    [StringLength(200, ErrorMessage = "标题不能超过 200 字")]
    public string Title { get; set; } = string.Empty;

    /// <summary>公告内容（富文本 HTML，可为空）</summary>
    public string? Content { get; set; }

    /// <summary>是否置顶：1-置顶 0-不置顶</summary>
    public int IsTop { get; set; }

    /// <summary>状态：1-发布 0-草稿</summary>
    public int Status { get; set; }
}

/// <summary>
/// 切换发布 / 草稿状态、设置置顶入参
/// </summary>
public class AnnouncementStatusDto
{
    /// <summary>公告ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "公告ID非法")]
    public long Id { get; set; }

    /// <summary>状态：1-发布 0-草稿（不传则保持原值）</summary>
    public int? Status { get; set; }

    /// <summary>是否置顶：1-置顶 0-不置顶（不传则保持原值）</summary>
    public int? IsTop { get; set; }
}
