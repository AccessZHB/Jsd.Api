namespace Jsd.Api.Models.Home;

/// <summary>
/// 首页轮播图输出 DTO（小程序端直接用）
/// </summary>
public class BannerDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>轮播标题</summary>
    public string? Title { get; set; }

    /// <summary>图片完整访问URL</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>点击跳转的小程序页面路径（null 表示不跳转）</summary>
    public string? Path { get; set; }

    /// <summary>排序号</summary>
    public int Sort { get; set; }
}
