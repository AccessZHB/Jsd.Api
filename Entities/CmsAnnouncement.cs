using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 内容管理 - 公告表 cms_announcement
///
/// 用途：后台发布运营公告，小程序首页公告栏读取"已发布(status=1)"中的最新 / 置顶(is_top=1)一条滚动展示。
/// content 为富文本（HTML）；小程序端会剥离 HTML 标签仅保留纯文字滚动。
/// 表按需求文档字段定义映射（id / title / content / is_top / status / create_time / update_time）。
/// </summary>
[Table("cms_announcement")]
public class CmsAnnouncement
{
    /// <summary>主键ID（自增）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>公告标题（VARCHAR(200)）</summary>
    [Required]
    [StringLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>公告内容（富文本 HTML，TEXT）</summary>
    [Column("content")]
    public string? Content { get; set; }

    /// <summary>是否置顶：1-置顶 0-不置顶</summary>
    [Column("is_top")]
    public int IsTop { get; set; } = 0;

    /// <summary>状态：1-发布 0-草稿</summary>
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
