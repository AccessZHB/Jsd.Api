using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 登录锁定到期清理任务（自动任务模块）—— 清理 sys_user.lock_until 已过期的锁定记录。
///
/// 系统管理模块的登录失败锁定把过期时间落在 sys_user.lock_until 列上，但到期后不会自行复位，
/// 由本任务统一解锁，避免「锁定时间已过却仍然登不进去」。
///
/// 任务参数 job_params（JSON）：{ "retainDays": 30 } —— 只清理 N 天之前的锁定，默认 30 天。
/// </summary>
public class LoginLockCleanJob : IJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<LoginLockCleanJob> _logger;

    public LoginLockCleanJob(AppDbContext db, ILogger<LoginLockCleanJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var fireKey = JobDataHelper.ResultKey(context);
        var retainDays = JobDataHelper.GetInt(context.JobDetail.JobDataMap, "retainDays", 30);
        var threshold = DateTime.Now.AddDays(-Math.Max(retainDays, 0));

        // 批量 UPDATE 直接下推，避免把账号行整表拉进内存
        var affected = await _db.SysUsers
            .Where(x => x.LockUntil != null && x.LockUntil < threshold)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockUntil, (DateTime?)null));

        var msg = $"解锁过期登录锁定 {affected} 个账号（保留 {retainDays} 天内的锁定记录）";
        _logger.LogInformation("{Message}", msg);
        JobResultBuffer.Push(fireKey, msg);
    }
}
