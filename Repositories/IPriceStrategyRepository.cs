using Jsd.Api.Entities;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Repositories;

/// <summary>
/// 价格策略 仓储接口（price_strategy）
/// </summary>
public interface IPriceStrategyRepository : IRepository<PriceStrategy>
{
    /// <summary>分页查询策略（类型、状态、关键字、日期范围筛选）</summary>
    Task<(List<PriceStrategy> Items, int Total)> GetPagedListAsync(PriceStrategyQueryDto q);

    /// <summary>策略编号是否已存在（唯一性校验）</summary>
    Task<bool> ExistsStrategyNoAsync(string strategyNo);

    /// <summary>生成策略编号：PS + yyyyMMddHHmmss + 4 位随机</summary>
    string GenerateStrategyNo();

    /// <summary>根据ID取可跟踪实体（更新/状态流转用）</summary>
    Task<PriceStrategy?> GetByIdTrackedAsync(long id);

    /// <summary>
    /// 取当前处于生效窗口内的启用策略（按业务优先级、priority 降序返回），
    /// 供取价引擎按序匹配。
    /// </summary>
    Task<List<PriceStrategy>> GetEffectiveStrategiesAsync(DateTime now);

    /// <summary>统计策略下的规则条数（批量，避免 N+1）</summary>
    Task<Dictionary<long, int>> CountRulesAsync(List<long> strategyIds);
}
