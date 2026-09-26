using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jsd.Api.Services;

/// <summary>
/// 应用启动后的字典缓存预热（IHostedService）。
///
/// 作用：把全部启用中的字典类型一次性装载进缓存，避免第一个用户请求时回源数据库造成冷启动慢。
/// 失败不影响应用启动 —— 预热只是优化项，未命中缓存会自动回源。
///
/// 注意：IHostedService 是单例，而字典服务是 Scoped，这里用 IServiceScopeFactory 开短作用域。
/// </summary>
public class DictCachePreloadService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DictCachePreloadService> _logger;

    public DictCachePreloadService(IServiceScopeFactory scopeFactory, ILogger<DictCachePreloadService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<IDictCacheService>();

            await cacheService.PreloadAllAsync();

            _logger.LogInformation("[DictCache] 字典缓存预热完成");
        }
        catch (Exception ex)
        {
            // 预热异常绝不能阻断应用启动
            _logger.LogWarning(ex, "[DictCache] 字典缓存预热失败，将在首次请求时回源数据库");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
