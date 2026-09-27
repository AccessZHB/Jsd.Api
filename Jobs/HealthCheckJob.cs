using System.Diagnostics;
using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 系统健康检查任务（自动任务模块）—— 定期探一次「数据库连通性 + 进程内存占用」。
/// 结果写入 sys_job_log.execute_result；数据库不可达时抛异常，由 JobLogListener 记为失败。
/// </summary>
public class HealthCheckJob : IJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<HealthCheckJob> _logger;

    public HealthCheckJob(AppDbContext db, ILogger<HealthCheckJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var fireKey = JobDataHelper.ResultKey(context);
        var sw = Stopwatch.StartNew();

        // 一条极轻量的聚合查询即可判断连接池 / 网络是否正常
        var lastJobUpdate = await _db.SysJobs.MaxAsync(x => (DateTime?)x.UpdateTime);
        var dbMsg = lastJobUpdate is { } t
            ? $"数据库可连接，最新任务更新时间 {t:yyyy-MM-dd HH:mm:ss}"
            : "数据库可连接，暂无任务记录";

        var proc = Process.GetCurrentProcess();
        var memMsg = $"进程内存 {proc.WorkingSet64 / 1024 / 1024} MB，托管堆 {GC.GetTotalMemory(false) / 1024 / 1024} MB";

        sw.Stop();
        var msg = $"{dbMsg}；{memMsg}；检查耗时 {sw.ElapsedMilliseconds} ms";
        _logger.LogInformation("系统健康检查：{Message}", msg);
        JobResultBuffer.Push(fireKey, msg);
    }
}
