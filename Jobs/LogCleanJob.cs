using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 日志清理任务（自动任务模块）—— 按保留天数物理删除过期日志表数据。
///
/// 任务参数 job_params（JSON）：
/// { "logType": "login" | "operation" | "job", "retainDays": 180 }
///   · login     → sys_login_log
///   · operation → sys_operation_log
///   · job       → sys_job_log（自身日志也清理，默认 180 天）
///   · retainDays 缺失或非法时取 180 天；&lt;= 0 视为不限制，直接跳过。
///
/// 用 ExecuteDeleteAsync 一次性下发 DELETE，避免把历史数据整表拉进内存再删。
/// </summary>
public class LogCleanJob : IJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogCleanJob> _logger;

    public LogCleanJob(AppDbContext db, ILogger<LogCleanJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var map = context.JobDetail.JobDataMap;
        var logType = JobDataHelper.GetString(map, "logType") ?? "operation";
        var retainDays = JobDataHelper.GetInt(map, "retainDays", 180);
        var fireKey = JobDataHelper.ResultKey(context);

        if (retainDays <= 0)
        {
            var skip = $"logType={logType}，retainDays={retainDays}（0 表示不限制），跳过清理";
            _logger.LogInformation("日志清理任务：{Message}", skip);
            JobResultBuffer.Push(fireKey, skip);
            return;
        }

        var threshold = DateTime.Now.AddDays(-retainDays);
        var deleted = logType switch
        {
            "login" => await _db.SysLoginLogs.Where(x => x.CreateTime < threshold).ExecuteDeleteAsync(),
            "job" => await _db.SysJobLogs.Where(x => x.CreateTime < threshold).ExecuteDeleteAsync(),
            _ => await _db.SysOperationLogs.Where(x => x.CreateTime < threshold).ExecuteDeleteAsync(),
        };

        // 没配 job_params 就按默认值跑了，在结果里说明一下，免得使用者以为日志是被误删的
        var suffix = JobDataHelper.Has(map, "logType") || JobDataHelper.Has(map, "retainDays")
            ? string.Empty
            : "（未配置 job_params，已使用默认值）";

        var msg = $"已清理 {logType} 日志 {deleted} 条（保留 {retainDays} 天，截止 {threshold:yyyy-MM-dd HH:mm:ss}）{suffix}";
        _logger.LogInformation("日志清理任务执行完成：{Message}", msg);
        JobResultBuffer.Push(fireKey, msg);
    }
}
