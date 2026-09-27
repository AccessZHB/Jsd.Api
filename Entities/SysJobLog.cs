using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 任务执行日志表 sys_job_log（自动任务模块）
///
/// ⚠️【表结构以 Jsd_order.sql（2555 行 CREATE TABLE sys_job_log）与真实库为准】
/// 1) duration：BIGINT UNSIGNED NOT NULL DEFAULT 0 —— 用 long 承接（耗时毫秒，不会越界）。
/// 2) execute_result / error_message：TEXT 可空。
/// 3) status：TINYINT，1-成功 0-失败（注意与 sys_job.status 的 0/1 语义相反）。
/// 4) job_id 可空：Quartz 冷启动阶段 Trigger 对应的 sys_job 行可能已被删除，
///    为保留执行痕迹这里不建物理外键，由 Service 层做保护性写入。
/// </summary>
[Table("sys_job_log")]
public class SysJobLog
{
    /// <summary>日志主键</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联任务ID（sys_job.id，任务删除后保留痕迹故未建物理外键）</summary>
    [Column("job_id")]
    public long? JobId { get; set; }

    /// <summary>任务名称（冗余存储，sys_job 被删后仍可读）</summary>
    [Required]
    [StringLength(100)]
    [Column("job_name")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>任务分组</summary>
    [Required]
    [StringLength(100)]
    [Column("job_group")]
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>Quartz 触发器名称</summary>
    [StringLength(200)]
    [Column("trigger_name")]
    public string? TriggerName { get; set; }

    /// <summary>Quartz 触发器分组</summary>
    [StringLength(200)]
    [Column("trigger_group")]
    public string? TriggerGroup { get; set; }

    /// <summary>触发时间（Quartz 判定该次该触发的时刻）</summary>
    [Column("fire_time")]
    public DateTime FireTime { get; set; }

    /// <summary>开始执行时间</summary>
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>结束执行时间（任务未结束或崩溃时为 null）</summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>执行耗时（毫秒）</summary>
    [Column("duration")]
    public long Duration { get; set; }

    /// <summary>执行状态：1-成功 0-失败</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>执行结果摘要（成功时记录业务输出，超长自动截断）</summary>
    [Column("execute_result")]
    public string? ExecuteResult { get; set; }

    /// <summary>异常信息 / 堆栈（超长自动截断）</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>记录创建时间（数据库 CURRENT_TIMESTAMP）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; }
}
