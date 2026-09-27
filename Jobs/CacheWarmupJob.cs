using Jsd.Api.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 缓存预热任务（自动任务模块）—— 应用重启 / 缓存被清空后，把字典与系统配置重新装载进缓存。
/// 复用与启动时 DictCachePreloadService / SysConfigPreloadService 相同的预热入口，不做重复实现。
/// </summary>
public class CacheWarmupJob : IJob
{
    private readonly IDictCacheService _dictCache;
    private readonly ISysConfigService _configCache;
    private readonly ILogger<CacheWarmupJob> _logger;

    public CacheWarmupJob(
        IDictCacheService dictCache,
        ISysConfigService configCache,
        ILogger<CacheWarmupJob> logger)
    {
        _dictCache = dictCache;
        _configCache = configCache;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var fireKey = JobDataHelper.ResultKey(context);

        try
        {
            await _dictCache.PreloadAllAsync();
        }
        catch (Exception ex)
        {
            // 预热不允许因为一个源失败就整体中断，其余照常继续
            _logger.LogWarning(ex, "字典缓存预热失败（不影响配置预热）");
        }

        try
        {
            await _configCache.PreloadAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "系统配置缓存预热失败（不影响字典预热）");
        }

        var msg = "字典缓存与系统配置缓存预热完成";
        _logger.LogInformation("缓存预热任务执行完成：{Message}", msg);
        JobResultBuffer.Push(fireKey, msg);
    }
}
