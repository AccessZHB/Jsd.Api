using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 价格变更日志表 price_change_log（价格策略与会员余额模块）
///
/// 【审计定位】价格是交易链路的敏感数据，任何策略/规则的写操作都必须留痕：
///   新增规则、修改价格、停用策略、删除规则等，逐字段记录 old_value / new_value。
/// 【写入时机】与业务数据在同一事务内提交（PriceService 内统一调用），
///   禁止在 Service 之外单独写入，避免"价格改了但没日志"。
/// </summary>
[Table("price_change_log")]
public class PriceChangeLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>策略ID（price_strategy.id，可空：仅规则级变更时可空）</summary>
    [Column("strategy_id")]
    public long? StrategyId { get; set; }

    /// <summary>规则ID（price_rule.id，可空：仅策略级变更时可空）</summary>
    [Column("rule_id")]
    public long? RuleId { get; set; }

    /// <summary>操作类型：1-新增 2-修改 3-停用 4-删除</summary>
    [Column("operation_type")]
    public int OperationType { get; set; }

    /// <summary>变更字段（如 calc_value / status；整条新增时可填 "ALL"）</summary>
    [StringLength(50)]
    [Column("field_name")]
    public string? FieldName { get; set; }

    /// <summary>旧值</summary>
    [StringLength(255)]
    [Column("old_value")]
    public string? OldValue { get; set; }

    /// <summary>新值</summary>
    [StringLength(255)]
    [Column("new_value")]
    public string? NewValue { get; set; }

    /// <summary>操作人ID（sys_user.id）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>操作时间</summary>
    [Column("operate_time")]
    public DateTime OperateTime { get; set; }

    /// <summary>变更原因</summary>
    [StringLength(255)]
    [Column("reason")]
    public string? Reason { get; set; }
}
