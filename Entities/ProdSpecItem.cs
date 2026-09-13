using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品规格项表 prod_spec_item
/// 定义商品的可选规格维度（如折射率、膜层、颜色），仅用于后台配置和前端展示选择，
/// 不承载库存/价格（库存价格绑定在 prod_sku 上）。
/// </summary>
[Table("prod_spec_item")]
public class ProdSpecItem
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>商品ID（数据库列名 prod_info_id，非 product_id；关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>规格项名称（如折射率、膜层、颜色）</summary>
    [Required]
    [StringLength(50)]
    [Column("spec_name")]
    public string SpecName { get; set; } = string.Empty;

    /// <summary>规格类型：1-单选 2-输入</summary>
    [Column("spec_type")]
    public int SpecType { get; set; } = 1;

    /// <summary>是否必填：1-是 0-否</summary>
    [Column("is_required")]
    public int IsRequired { get; set; }

    /// <summary>排序号（数据库列名 sort，非 sort_order）</summary>
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
