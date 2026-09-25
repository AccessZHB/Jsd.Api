using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 价格策略主表 price_strategy（价格策略与会员余额模块）
///
/// 【策略类型 strategy_type】1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价
/// 【状态 status】0-草稿 1-启用 2-停用
///   仅草稿可修改基本信息；提交（submit）时校验规则完整性后转为启用；停用后不再参与取价。
/// 【优先级 priority】数值越大越优先。取价引擎先按 strategy_type 的业务优先级分流，
///   同类型内再按 priority 降序取第一个命中的规则（见 PriceService.QuotationAsync）。
/// 【有效期】effective_date / expire_date 共同决定是否处于生效窗口。
/// </summary>
[Table("price_strategy")]
public class PriceStrategy
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>策略编号（唯一，PS + yyyyMMddHHmmss + 4位随机数，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("strategy_no")]
    public string StrategyNo { get; set; } = string.Empty;

    /// <summary>策略名称</summary>
    [Required]
    [StringLength(100)]
    [Column("strategy_name")]
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>策略类型：1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价</summary>
    [Column("strategy_type")]
    public int StrategyType { get; set; }

    /// <summary>优先级（数值越大越优先）</summary>
    [Column("priority")]
    public int Priority { get; set; }

    /// <summary>生效时间</summary>
    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    /// <summary>失效时间</summary>
    [Column("expire_date")]
    public DateTime ExpireDate { get; set; }

    /// <summary>状态：0-草稿 1-启用 2-停用</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>
    /// 策略描述 / 备注。
    /// ⚠️ 数据库列名是 remark（见 Jsd_order.sql 中 price_strategy 建表语句），
    ///    与需求文档中的 description 不一致，此处一律以【数据库】为准。
    /// </summary>
    [StringLength(255)]
    [Column("remark")]
    public string? Description { get; set; }

    /// <summary>创建人ID（sys_user.id）</summary>
    [Column("create_by")]
    public long CreateBy { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>策略下的规则集合（批量维护时全量覆盖）</summary>
    public virtual List<PriceRule> Rules { get; set; } = new();
}
