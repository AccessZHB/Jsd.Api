using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jsd.Api.Services;

/// <summary>
/// 应用启动后的系统配置缓存预热（IHostedService）。
///
/// 作用：把全部配置一次性装载进缓存，避免第一个请求回源数据库造成冷启动慢。
/// 失败不影响应用启动 —— 预热只是优化项，未命中缓存会自动回源数据库，
/// 所以即使 Kestrel 先开始监听、预热后完成，也不会读到错误数据。
///
/// 注意：IHostedService 是单例，配置服务是 Scoped，这里用 IServiceScopeFactory 开短作用域。
/// </summary>
public class SysConfigPreloadService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SysConfigPreloadService> _logger;

    public SysConfigPreloadService(IServiceScopeFactory scopeFactory, ILogger<SysConfigPreloadService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<ISysConfigService>();

            var count = await configService.PreloadAllAsync();

            _logger.LogInformation("[SysConfig] 系统配置缓存预热完成，共载入 {Count} 条", count);
        }
        catch (Exception ex)
        {
            // 预热异常绝不能阻断应用启动
            _logger.LogWarning(ex, "[SysConfig] 系统配置缓存预热失败，将在首次读取时回源数据库");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
