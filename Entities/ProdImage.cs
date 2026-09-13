using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品图片表 prod_image（原名 pro_image，已重命名）
/// image_type：'1'-主图（仅1张） '2'-轮播图（多张，按 sort 排序） '3'-详情图（多张）
/// </summary>
[Table("prod_image")]
public class ProdImage
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联商品ID（prod_info.id，可空）</summary>
    [Column("prod_info_id")]
    public long? ProdInfoId { get; set; }

    /// <summary>图片类型：'1'-主图 '2'-轮播图 '3'-详情图</summary>
    [StringLength(32)]
    [Column("image_type")]
    public string? ImageType { get; set; }

    /// <summary>原图完整访问URL</summary>
    [Required]
    [StringLength(512)]
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>排序号（多张图片展示顺序，越小越靠前）</summary>
    [Column("sort")]
    public int? Sort { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
