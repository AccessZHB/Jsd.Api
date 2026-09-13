using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 供应商基础信息表 sup_supplier（依据 Jsd_order.sql 逐列映射）
/// 商品通过 supplier_id 关联本表，可为空（无供应商）。
/// </summary>
[Table("sup_supplier")]
public class SupSupplier
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>供应商名称（如：依视路光学，VARCHAR(100)）</summary>
    [Required]
    [StringLength(100)]
    [Column("supplier_name")]
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>联系人</summary>
    [StringLength(50)]
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    [StringLength(20)]
    [Column("contact_phone")]
    public string? ContactPhone { get; set; }

    /// <summary>合作状态：1-启用 0-禁用</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
