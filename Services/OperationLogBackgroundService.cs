using Jsd.Api.Entities;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 操作日志后台写入服务：从 Channel 消费日志实体，逐条落库 sys_operation_log。
/// 消费失败（如数据库抖动）只打日志不抛出 —— 审计日志不能影响业务进程存活。
/// </summary>
public class OperationLogBackgroundService : BackgroundService
{
    private readonly OperationLogWriter _writer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationLogBackgroundService> _logger;

    public OperationLogBackgroundService(
        OperationLogWriter writer,
        IServiceScopeFactory scopeFactory,
        ILogger<OperationLogBackgroundService> logger)
    {
        _writer = writer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _writer.Reader;

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            while (reader.TryRead(out var log))
            {
                try
                {
                    // BackgroundService 是单例，仓储是 Scoped —— 每条日志开一个短作用域取仓储
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ISysOperationLogRepository>();
                    await repo.AddAsync(log);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "操作日志落库失败：module={Module}, action={Action}, path={Path}",
                        log.Module, log.Action, log.Path);
                }
            }
        }
    }
}
