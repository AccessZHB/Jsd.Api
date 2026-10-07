using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 首页轮播图（运营位）表 mkt_banner
///
/// 用途：小程序首页顶部轮播。后台维护图片、标题与跳转路径，
/// 小程序端按 sort 升序取「启用」状态的记录展示。
///
/// Path 约定（小程序内部页面路径，前端 uni.navigateTo 直接用）：
///   留空或 null            → 点击不跳转
///   /pages/category/list   → 跳分类列表
///   /pages/product/detail?id=1 → 跳商品详情
/// </summary>
[Table("mkt_banner")]
public class MktBanner
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>轮播标题（可空，图片上叠加展示）</summary>
    [StringLength(100)]
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>图片完整访问URL（必填）</summary>
    [Required]
    [StringLength(512)]
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>点击跳转的小程序页面路径（可空表示不跳转）</summary>
    [StringLength(255)]
    [Column("path")]
    public string? Path { get; set; }

    /// <summary>排序号（越小越靠前，默认 0）</summary>
    [Column("sort")]
    public int Sort { get; set; } = 0;

    /// <summary>状态：1-启用 0-禁用（小程序只取启用的）</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
