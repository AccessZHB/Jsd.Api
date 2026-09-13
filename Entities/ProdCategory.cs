using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品分类表 prod_category（依据 Jsd_order.sql 逐列映射，支持多级分类）
/// </summary>
[Table("prod_category")]
public class ProdCategory
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>父分类ID（0 = 顶级分类）</summary>
    [Column("parent_id")]
    public long ParentId { get; set; }

    /// <summary>分类名称（VARCHAR(50)）</summary>
    [Required]
    [StringLength(50)]
    [Column("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>分类类型：1-镜片 2-镜架 3-营养品 4-补光设备 5-视觉训练设备 6-远像设备</summary>
    [Column("category_type")]
    public int? CategoryType { get; set; }

    /// <summary>分类图标URL</summary>
    [StringLength(255)]
    [Column("icon")]
    public string? Icon { get; set; }

    /// <summary>排序号（数据库列名 sort，非 sort_order；越小越靠前）</summary>
    [Column("sort")]
    public int? Sort { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
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
