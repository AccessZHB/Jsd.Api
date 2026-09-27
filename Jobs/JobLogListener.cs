using System.Collections.Concurrent;
using Jsd.Api.Entities;
using Jsd.Api.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 定时任务执行日志监听器（自动任务模块）—— 统一在 Quartz 的
/// JobToBeExecuted / JobWasExecuted / JobExecutionVetoed 三个事件里落 sys_job_log，
/// 业务 Job 自身完全不写日志（见文档「日志采集用 Listener 而非手动」）。
///
/// 之所以要自己开 DI 作用域：监听器是单例，而仓储 / DbContext 是按请求域注册的，
/// 直接用注入的 scoped 依赖会跨请求复用 DbContext，属于典型隐患。
///
/// 另外顺带实现任务级失败重试：JobWasExecuted 里发现异常且 retryLeft &gt; 0 时，
/// 按 retry_interval 秒后补一个 SimpleTrigger 重跑同一个 JobDetail（次数递减到 0 为止）。
/// </summary>
public class JobLogListener : IJobListener
{
    /// <summary>监听器名称（全局监听器，挂到 Scheduler 上对所有 Job 生效）</summary>
    public const string ListenerName = "sysJobLogListener";

    private static readonly ConcurrentDictionary<string, DateTime> StartTimes = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobLogListener> _logger;

    public JobLogListener(IServiceScopeFactory scopeFactory, ILogger<JobLogListener> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Name => ListenerName;

    /// <summary>任务即将执行：记录开始时刻</summary>
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (context.FireInstanceId is { Length: > 0 } id)
        {
            StartTimes[id] = DateTime.Now;
        }

        return Task.CompletedTask;
    }

    /// <summary>任务被否决（例如被并发策略或调度器策略拦下）：记一条失败日志</summary>
    public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var fireKey = JobDataHelper.ResultKey(context);
        JobResultBuffer.Drop(fireKey);

        await WriteAsync(context, exception: new InvalidOperationException("任务执行被调度器否决（JobExecutionVetoed）"));
    }

    /// <summary>任务执行完毕（无论成功失败）：落日志；失败且还有重试次数时补发一次重跑</summary>
    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? exception, CancellationToken cancellationToken = default)
    {
        var fireKey = JobDataHelper.ResultKey(context);
        var result = JobResultBuffer.Take(fireKey);
        var start = StartTimes.TryRemove(fireKey, out var s) ? s : DateTime.Now;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logRepo = scope.ServiceProvider.GetRequiredService<IRepository<SysJobLog>>();
            var jobRepo = scope.ServiceProvider.GetRequiredService<ISysJobRepository>();

            var log = BuildLog(context, exception, start, result);

            // ⚠️ JobId 必须回填：Quartz 侧只有 JobKey（name+group），拿不到 sys_job.id。
            // 不回填的话 sys_job_log.job_id 恒为 NULL，日志页按任务筛选永远查不到任何记录。
            // 任务已被删除时查不到属属正常，保持 NULL 以保留执行痕迹。
            var jobKey = context.JobDetail.Key;
            var sysJob = await jobRepo.FindByNameAndGroupAsync(jobKey.Name, jobKey.Group);
            log.JobId = sysJob?.Id;

            await logRepo.AddAsync(log);
            await logRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // 日志落库失败不能反过来把任务结果弄成失败
            _logger.LogError(ex, "定时任务执行日志落库失败（job={JobKey}）", context.JobDetail.Key);
        }

        if (exception is null) return;

        // ---------- 失败重试 ----------
        var map = context.JobDetail.JobDataMap;
        var retryLeft = JobDataHelper.GetInt(map, "retryLeft", 0);
        var retryInterval = JobDataHelper.GetInt(map, "retryInterval", 0);

        if (retryLeft <= 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();

            var retryMap = new JobDataMap();
            foreach (var key in map.Keys) retryMap.Put(key, map.Get(key));
            retryMap.Put("retryLeft", retryLeft - 1);

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{context.Trigger.Key.Name}#retry{retryLeft - 1}", context.Trigger.Key.Group)
                .ForJob(context.JobDetail.Key)
                .UsingJobData(retryMap)
                .StartAt(DateTimeOffset.Now.AddSeconds(Math.Max(retryInterval, 0)))
                .Build();

            await scheduler.ScheduleJob(trigger, cancellationToken);
            _logger.LogInformation("定时任务失败重试：job={JobKey} 剩余 {Retry} 次，间隔 {Interval}s",
                context.JobDetail.Key, retryLeft - 1, Math.Max(retryInterval, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时任务失败重试排程失败（job={JobKey}）", context.JobDetail.Key);
        }
    }

    // ------------------------------------------------------------------

    private static SysJobLog BuildLog(
        IJobExecutionContext context,
        Exception? exception,
        DateTime start,
        string? result)
    {
        var jobKey = context.JobDetail.Key;

        var end = DateTime.Now;
        var startedAt = start;

        string? errorMessage = null;
        if (exception is not null)
        {
            var text = exception is JobExecutionException jee && jee.InnerException is not null
                ? jee.InnerException.ToString()
                : exception.ToString();
            errorMessage = text.Length > 2000 ? "…" + text[^2000..] : text;
        }

        return new SysJobLog
        {
            JobName = jobKey.Name,
            JobGroup = jobKey.Group,
            TriggerName = context.Trigger.Key.Name,
            TriggerGroup = context.Trigger.Key.Group,
            FireTime = context.FireTimeUtc.ToLocalTime().DateTime,
            StartTime = startedAt,
            EndTime = end,
            Duration = (long)((end - startedAt).TotalMilliseconds),
            Status = exception is null ? 1 : 0,
            ExecuteResult = result,
            ErrorMessage = errorMessage,
        };
    }

    private async Task WriteAsync(IJobExecutionContext context, Exception exception)
    {
        using var scope = _scopeFactory.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<IRepository<SysJobLog>>();

        var log = BuildLog(context, exception, DateTime.Now, null);
        var jobKey = context.JobDetail.Key;
        var sysJob = await scope.ServiceProvider
            .GetRequiredService<ISysJobRepository>()
            .FindByNameAndGroupAsync(jobKey.Name, jobKey.Group);
        log.JobId = sysJob?.Id;

        await logRepo.AddAsync(log);
        await logRepo.SaveChangesAsync();
    }
}
