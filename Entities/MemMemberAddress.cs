using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 会员收货地址表 mem_member_address（小程序确认订单页「新增/修改收货地址」落库）
///
/// 【会员维度隔离】member_id 关联 mem_member.id，与后台管理员 sys_user 物理隔离。
/// 字段对齐前端 Address 模型（province/city/district/detail + name/phone/is_default），
/// 直接映射 trx_order 的 receiver_province/city/district/address，下单无需再做地址拼接。
/// </summary>
[Table("mem_member_address")]
public class MemMemberAddress
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>会员ID（关联 mem_member.id）</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    /// <summary>收货人姓名</summary>
    [StringLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>收货人电话</summary>
    [StringLength(20)]
    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>省</summary>
    [StringLength(50)]
    [Column("province")]
    public string Province { get; set; } = string.Empty;

    /// <summary>市</summary>
    [StringLength(50)]
    [Column("city")]
    public string City { get; set; } = string.Empty;

    /// <summary>区/县</summary>
    [StringLength(50)]
    [Column("district")]
    public string District { get; set; } = string.Empty;

    /// <summary>详细地址</summary>
    [StringLength(255)]
    [Column("detail")]
    public string Detail { get; set; } = string.Empty;

    /// <summary>是否默认地址：0-否 1-是</summary>
    [Column("is_default")]
    public int IsDefault { get; set; }

    /// <summary>逻辑删除：0-正常 1-删除</summary>
    [Column("is_deleted")]
    public int IsDeleted { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
