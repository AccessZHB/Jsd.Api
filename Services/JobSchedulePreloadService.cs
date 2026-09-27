using Jsd.Api.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Services;

/// <summary>
/// 应用启动后把 sys_job 里 status=1 的任务注册进 Quartz 调度器（IHostedService）。
///
/// 与 DictCachePreloadService / SysConfigPreloadService 同一套路：
/// IHostedService 是单例，而 JobService / DbContext 是 Scoped，必须开短作用域取。
///
/// 注意：Quartz 的 QuartzHostedService 会先启动调度器并把 IScheduler 变成可用状态，
/// 本服务注册在它之后（Program.cs 里的 AddQuartzHostedService 在前），
/// 因此这里拿到的 IScheduler 已经 ready，可以直接 ScheduleJob。
/// </summary>
public class JobSchedulePreloadService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobSchedulePreloadService> _logger;

    public JobSchedulePreloadService(IServiceScopeFactory scopeFactory, ILogger<JobSchedulePreloadService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

            // Quartz 3.12 已移除 AddQuartzListener 扩展，这里手动把日志监听器挂到全局，
            // 之后所有 Job 的执行结果都会落到 sys_job_log（含失败重试逻辑）。
            // 监听器本体需要 scoped 依赖（仓储 / DbContext），所以在短作用域内创建。
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();
            scheduler.ListenerManager.AddJobListener(
                ActivatorUtilities.CreateInstance<JobLogListener>(scope.ServiceProvider));

            // IJobService 里没有暴露预热方法，这里按接口取具体实现再调用。
            // 之所以强转：预热是启动期内部动作，不对外暴露成 HTTP 接口。
            if (jobService is JobService impl)
            {
                await impl.PreloadEnabledJobsAsync();
            }
        }
        catch (Exception ex)
        {
            // 预热失败不能阻断应用启动
            _logger.LogError(ex, "定时任务预热异常（应用继续启动，任务将不会自动调度）");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
