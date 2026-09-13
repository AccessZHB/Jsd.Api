using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockCheck;

namespace Jsd.Api.Services;

/// <summary>
/// 库存盘点服务接口
/// </summary>
public interface IStockCheckService
{
    /// <summary>分页查询盘点单（单号/状态/时间范围筛选 + 差异汇总）</summary>
    Task<ApiResponse<PagedResult<StockCheckListDto>>> GetPagedListAsync(
        string? keyword, int? status, DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>获取盘点单详情（主表 + 明细列表）</summary>
    Task<ApiResponse<StockCheckDetailDto>> GetDetailAsync(long id);

    /// <summary>创建盘点单（带入系统库存快照，状态=盘点中，写变动日志）</summary>
    Task<ApiResponse<object>> CreateAsync(StockCheckCreateDto dto);

    /// <summary>完成盘点（录入实盘数量，计算盈亏，调整库存，状态=已完成）</summary>
    Task<ApiResponse<object>> CompleteAsync(long id, StockCheckUpdateDto dto);

    /// <summary>删除盘点单（仅盘点中可删）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);
}
