using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 盘点单主表仓储接口
/// </summary>
public interface ILogStockCheckRepository : IRepository<LogStockCheck>
{
    /// <summary>
    /// 分页查询盘点单（支持单号模糊、状态、创建时间范围筛选）
    /// </summary>
    Task<(List<LogStockCheck> Items, int Total)> GetPagedListAsync(
        string? keyword, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>
    /// 根据ID查询盘点单详情（Include 明细列表）
    /// </summary>
    Task<LogStockCheck?> GetDetailAsync(long id);

    /// <summary>
    /// 判断盘点单号是否已存在（生成单号时去重）
    /// </summary>
    Task<bool> CheckNoExistsAsync(string checkNo);
}
