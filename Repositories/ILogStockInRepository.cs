using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 入库单主表仓储接口
/// </summary>
public interface ILogStockInRepository : IRepository<LogStockIn>
{
    /// <summary>
    /// 分页查询入库单（支持单号模糊、供应商、状态、创建时间范围筛选）
    /// </summary>
    /// <param name="keyword">入库单号关键词（可空）</param>
    /// <param name="supplierId">供应商ID（可空）</param>
    /// <param name="status">状态：0-待入库 1-已完成 2-已取消（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>(当前页数据, 总条数)</returns>
    Task<(List<LogStockIn> Items, int Total)> GetPagedListAsync(
        string? keyword, long? supplierId, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>
    /// 根据ID查询入库单详情（Include 供应商 + 明细列表）
    /// </summary>
    Task<LogStockIn?> GetDetailAsync(long id);

    /// <summary>
    /// 判断入库单号是否已存在（生成单号时去重）
    /// </summary>
    Task<bool> StockInNoExistsAsync(string stockInNo);
}
