using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Home;

/// <summary>
/// 后台轮播图列表项（管理页分页列表用）
/// 相比小程序公开 BannerDto，多了 status / createTime / updateTime 三个后台字段。
/// </summary>
public class BannerListDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>轮播标题（可空）</summary>
    public string? Title { get; set; }

    /// <summary>图片完整访问URL（必填，存储相对 /upload/... 或 http(s) 完整地址）</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>点击跳转的小程序页面路径（可空）</summary>
    public string? Path { get; set; }

    /// <summary>排序号（越小越靠前）</summary>
    public int Sort { get; set; }

    /// <summary>状态：1-启用 0-禁用</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 新增 / 修改轮播图入参（Id=0 新增，&gt;0 修改）
/// </summary>
public class BannerSaveDto
{
    /// <summary>轮播ID（新增时传 0）</summary>
    public long Id { get; set; }

    /// <summary>轮播标题（可选，≤100 字）</summary>
    [StringLength(100, ErrorMessage = "标题不能超过 100 字")]
    public string? Title { get; set; }

    /// <summary>轮播图片地址（必填，上传后相对 /upload/... 或完整 http(s) 地址）</summary>
    [Required(ErrorMessage = "轮播图片不能为空")]
    [StringLength(512, ErrorMessage = "图片地址过长")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>点击跳转的小程序页面路径（可选，如 /pages/category/list；留空表示不跳转）</summary>
    [StringLength(255, ErrorMessage = "跳转路径过长")]
    public string? Path { get; set; }

    /// <summary>排序号（越小越靠前，默认 0）</summary>
    public int Sort { get; set; }

    /// <summary>状态：1-启用 0-禁用（默认启用）</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 切换启用 / 禁用状态入参
/// </summary>
public class BannerStatusDto
{
    /// <summary>轮播ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "轮播ID非法")]
    public long Id { get; set; }

    /// <summary>状态：1-启用 0-禁用</summary>
    [Required(ErrorMessage = "状态不能为空")]
    [Range(0, 1, ErrorMessage = "状态取值非法")]
    public int Status { get; set; }
}
