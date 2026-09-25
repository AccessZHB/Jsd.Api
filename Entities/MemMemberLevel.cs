using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 客户（会员）等级表 mem_member_level（价格策略与会员余额模块）
///
/// 业务定位：等级是"客户等级价"策略的锚点。mem_member.customer_level_id 指向本表 id。
/// default_discount（默认折扣率）作为该等级客户的兜底折扣：
///   当客户没有任何命中的价格规则时，取价引擎会回退到「等级默认折扣」再回退到「标准售价」。
///
/// 【停用约束】等级停用前必须校验：是否仍存在引用该等级的启用中价格规则（见 PriceService），
/// 避免"停用等级 → 客户突然按原价下单"的静默事故。
/// </summary>
[Table("mem_member_level")]
public class MemMemberLevel
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>等级名称（如：金牌客户）</summary>
    [Required]
    [StringLength(50)]
    [Column("level_name")]
    public string LevelName { get; set; } = string.Empty;

    /// <summary>等级编码（唯一，如 GOLD；业务侧用于对接外部系统）</summary>
    [Required]
    [StringLength(20)]
    [Column("level_code")]
    public string LevelCode { get; set; } = string.Empty;

    /// <summary>排序权重（数值越大越靠前）</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>默认折扣率（decimal(5,4)：0.8500 表示 85 折；1.0000 表示不打折）</summary>
    [Column("default_discount", TypeName = "decimal(5,4)")]
    public decimal DefaultDiscount { get; set; } = 1.0000m;

    /// <summary>等级描述</summary>
    [StringLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
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
