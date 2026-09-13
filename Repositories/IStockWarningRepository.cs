using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 库存预警配置仓储接口（继承泛型仓储，额外提供带当前库存的分页查询与重复配置校验）。
/// </summary>
public interface IStockWarningRepository : IRepository<LogStockWarning>
{
    /// <summary>
    /// 分页查询预警配置，并带出"当前库存"。
    /// 当前库存用相关子查询在 SQL 侧算好（SKU级取该SKU库存，商品级取该商品SKU库存之和），
    /// 这样 onlyWarning 筛选也能在数据库层完成，分页 total 才准确。
    /// </summary>
    /// <param name="prodInfoId">商品ID筛选（可空）</param>
    /// <param name="status">状态筛选：1-启用 0-停用（可空=全部）</param>
    /// <param name="onlyWarning">true=仅返回已触发预警且启用的配置（可空）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条数</param>
    Task<(List<StockWarningWithStock> Items, int Total)> GetPagedListAsync(
        long? prodInfoId, int? status, bool? onlyWarning, int page, int pageSize);

    /// <summary>
    /// 查询当前库存：skuId 有值取该 SKU 库存；为空取该商品全部 SKU 库存之和。
    /// </summary>
    Task<int> GetCurrentStockAsync(long prodInfoId, long? skuId);

    /// <summary>
    /// 是否已存在同一商品 + 同一 SKU 的预警配置（用于参数校验：一个商品的一个SKU只能配一条）。
    /// </summary>
    /// <param name="prodInfoId">商品ID</param>
    /// <param name="skuId">SKU ID（null 表示校验商品级配置）</param>
    /// <param name="excludeId">编辑时排除自身ID（可空）</param>
    Task<bool> ExistsConflictAsync(long prodInfoId, long? skuId, long? excludeId);
}

/// <summary>
/// 仓储层中间结果：预警配置实体 + 实时计算出的当前库存。
/// 单独定义成类（而非匿名类型）才能在方法签名中传递。
/// </summary>
public class StockWarningWithStock
{
    /// <summary>预警配置实体</summary>
    public LogStockWarning Warning { get; set; } = null!;

    /// <summary>当前库存（SQL 侧子查询算出）</summary>
    public int CurrentStock { get; set; }
}
