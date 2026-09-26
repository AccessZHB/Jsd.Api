using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 字典数据表 sys_dict_data（字典管理模块）
///
/// ⚠️【表结构以 Jsd_order.sql（约 2185 行）与真实库为准，与需求文档不同】
///   1) is_default：库里是 CHAR(1) 'Y'/'N'，不是文档的 TINYINT 1/0。Service 层已做布尔转换，
///      对外接口统一用 bool。
///   2) status：0-正常（启用） 1-停用（同 sys_dict_type）。
///   3) create_by / update_by：VARCHAR(64) 账号名。
///   4) 库索引为复合 idx_dict_type_id_sort(dict_type_id, dict_sort)，覆盖按类型排序查询。
/// </summary>
[Table("sys_dict_data")]
public class SysDictData
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联字典类型ID（sys_dict_type.id）</summary>
    [Required]
    [Column("dict_type_id")]
    public long DictTypeId { get; set; }

    /// <summary>字典标签（前端展示文本）</summary>
    [Required]
    [StringLength(100)]
    [Column("dict_label")]
    public string DictLabel { get; set; } = string.Empty;

    /// <summary>字典键值（存储值，前端拿它做 option.value）</summary>
    [Required]
    [StringLength(100)]
    [Column("dict_value")]
    public string DictValue { get; set; } = string.Empty;

    /// <summary>排序权重（数值越小越靠前）</summary>
    [Column("dict_sort")]
    public int DictSort { get; set; }

    /// <summary>CSS 样式类名（EL-TAG 的 effect 等扩展样式）</summary>
    [StringLength(100)]
    [Column("css_class")]
    public string CssClass { get; set; } = string.Empty;

    /// <summary>是否默认选中：Y-是 N-否（库里只有这两个值）</summary>
    [Required]
    [StringLength(1)]
    [Column("is_default")]
    public string IsDefault { get; set; } = "N";

    /// <summary>状态：0-正常（启用） 1-停用</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; } = string.Empty;

    /// <summary>创建人账号</summary>
    [StringLength(64)]
    [Column("create_by")]
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>创建时间（数据库 CURRENT_TIMESTAMP）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新人账号</summary>
    [StringLength(64)]
    [Column("update_by")]
    public string UpdateBy { get; set; } = string.Empty;

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime? UpdateTime { get; set; }
}
