using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 字典类型表 sys_dict_type（字典管理模块）
///
/// ⚠️【表结构以 Jsd_order.sql（约 2166 行）与真实库为准，与需求文档不同，勿按文档写】
///   1) status：库注释为「0-正常 1-停用」，默认值 0 ⇒ 0=启用、1=停用；
///      文档写的「1-启用 0-停用」与之相反，是错的。
///   2) create_by / update_by：VARCHAR(64) 账号名（"admin"），不是 BIGINT 用户ID。
///   3) 库里有 update_by，文档没有这个字段。
///   4) update_time 依赖数据库 ON UPDATE CURRENT_TIMESTAMP 自动维护，故标注 Computed。
/// </summary>
[Table("sys_dict_type")]
public class SysDictType
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>字典名称（如：订单状态、商品分类）</summary>
    [Required]
    [StringLength(100)]
    [Column("dict_name")]
    public string DictName { get; set; } = string.Empty;

    /// <summary>字典类型编码，全局唯一（如：sys_order_status）；库上有 uk_dict_type 唯一索引</summary>
    [Required]
    [StringLength(100)]
    [Column("dict_type")]
    public string DictType { get; set; } = string.Empty;

    /// <summary>状态：0-正常（启用） 1-停用</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

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

    /// <summary>更新时间（数据库 CURRENT_TIMESTAMP ON UPDATE，标注 Computed 避免 EF 覆盖）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime? UpdateTime { get; set; }
}
