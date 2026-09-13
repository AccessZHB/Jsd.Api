using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 出库单主表仓储接口
/// </summary>
public interface ILogStockOutRepository : IRepository<LogStockOut>
{
    /// <summary>
    /// 分页查询出库单（支持单号模糊、类型、状态、创建时间范围筛选）
    /// </summary>
    /// <param name="keyword">出库单号关键词（可空）</param>
    /// <param name="outType">出库类型：1-样品领用 2-报损 3-其他（可空=全部）</param>
    /// <param name="status">状态：0-待出库 1-已完成（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>(当前页数据, 总条数)</returns>
    Task<(List<LogStockOut> Items, int Total)> GetPagedListAsync(
        string? keyword, int? outType, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>
    /// 根据ID查询出库单详情（Include 明细列表）
    /// </summary>
    Task<LogStockOut?> GetDetailAsync(long id);

    /// <summary>
    /// 判断出库单号是否已存在（生成单号时去重）
    /// </summary>
    Task<bool> StockOutNoExistsAsync(string stockOutNo);
}
