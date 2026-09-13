using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品规格值表 prod_spec_value
/// 记录规格项的具体可选值（如折射率可选 1.56 / 1.60 / 1.67）。
/// </summary>
[Table("prod_spec_value")]
public class ProdSpecValue
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>规格项ID（数据库列名 spec_id，非 spec_item_id；关联 prod_spec_item.id）</summary>
    [Column("spec_id")]
    public long SpecId { get; set; }

    /// <summary>商品ID（冗余字段，便于按商品直接查询规格值，可空）</summary>
    [Column("prod_info_id")]
    public long? ProdInfoId { get; set; }

    /// <summary>规格值（如 1.60、防蓝光膜、C1）</summary>
    [Required]
    [StringLength(50)]
    [Column("spec_value")]
    public string SpecValue { get; set; } = string.Empty;

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
