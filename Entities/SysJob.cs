using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 定时任务定义表 sys_job（自动任务模块）
///
/// ⚠️【表结构以 Jsd_order.sql（2529 行 CREATE TABLE sys_job）与真实库为准】
/// 本表在 Jsd_order.sql 第三部分「12. sys_job」中定义，与文档描述一致。
/// 1) job_group：VARCHAR(100) NOT NULL DEFAULT 'DEFAULT'（列定义里是字面量 'DEFAULT'）。
/// 2) job_params：VARCHAR(500) NULL DEFAULT ''，可空 → C# 用可空 string。
/// 3) status：TINYINT，0-停止 1-运行中。Quartz 侧对应 Trigger 的 PAUSED / NORMAL。
/// 4) misfire_policy：TINYINT，0-立即执行(M now with existing repeat count) 1-执行一次
///    2-忽略(M now with existing repeat count 的 MISFIRE_INSTRUCTION_IGNORE_MISFIRE_POLICY)。
/// 5) create_time / update_time 由数据库默认值与 ON UPDATE 维护，标注 Computed 避免 EF 覆盖。
/// 6) 数据库无唯一索引（uk 只有一个 PRI），任务名 + 组唯一性由 Service 层校验。
/// </summary>
[Table("sys_job")]
public class SysJob
{
    /// <summary>任务主键</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>任务名称（如「登录日志清理任务」）</summary>
    [Required]
    [StringLength(100)]
    [Column("job_name")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>任务分组（Quartz JOB_GROUP，如 DEFAULT）</summary>
    [Required]
    [StringLength(100)]
    [Column("job_group")]
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>
    /// 任务类型 —— 全限定类名（反射调用，如 Jsd.Api.Jobs.LogCleanJob, Jsd.Api）
    /// 由 Type.GetType() 解析后交给 Quartz 的 JobBuilder.Create(Type)。
    /// </summary>
    [Required]
    [StringLength(200)]
    [Column("job_type")]
    public string JobType { get; set; } = string.Empty;

    /// <summary>Cron 表达式（Quartz 7 位语法，含秒）</summary>
    [Required]
    [StringLength(100)]
    [Column("cron_expression")]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>任务参数（JSON 字符串，如 {"logType":"login","retainDays":180}）</summary>
    [StringLength(500)]
    [Column("job_params")]
    public string? JobParams { get; set; }

    /// <summary>状态：0-停止 1-运行中</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>丢失策略：0-立即执行 1-执行一次 2-忽略</summary>
    [Column("misfire_policy")]
    public int MisfirePolicy { get; set; } = 1;

    /// <summary>是否允许并发：0-禁止（对应 Quartz @DisallowConcurrentExecution） 1-允许</summary>
    [Column("concurrent")]
    public int Concurrent { get; set; }

    /// <summary>任务超时时间（秒），0=不限制</summary>
    [Column("timeout")]
    public int Timeout { get; set; }

    /// <summary>失败重试次数</summary>
    [Column("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>失败重试间隔（秒）</summary>
    [Column("retry_interval")]
    public int RetryInterval { get; set; }

    /// <summary>创建者账号</summary>
    [Required]
    [StringLength(64)]
    [Column("create_by")]
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>创建时间（数据库 CURRENT_TIMESTAMP）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新者账号</summary>
    [Required]
    [StringLength(64)]
    [Column("update_by")]
    public string UpdateBy { get; set; } = string.Empty;

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }
}
