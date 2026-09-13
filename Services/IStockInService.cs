using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockIn;

namespace Jsd.Api.Services;

/// <summary>
/// 入库管理服务接口
/// </summary>
public interface IStockInService
{
    /// <summary>分页查询入库单（单号/供应商/状态/时间范围筛选）</summary>
    Task<ApiResponse<PagedResult<StockInListDto>>> GetPagedListAsync(
        string? keyword, long? supplierId, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>获取入库单详情（主表 + 明细列表）</summary>
    Task<ApiResponse<StockInDetailDto>> GetDetailAsync(long id);

    /// <summary>
    /// 创建入库单（事务：写主表 → 写明细 → 增加 SKU 库存 → 写库存变动日志），
    /// 创建即完成，status=1。
    /// </summary>
    Task<ApiResponse<object>> CreateAsync(StockInCreateDto dto);

    /// <summary>
    /// 编辑入库单（事务：旧明细冲减库存 → 删旧明细 → 写新明细 → 新明细增加库存）。
    /// </summary>
    Task<ApiResponse<object>> UpdateAsync(long id, StockInUpdateDto dto);

    /// <summary>删除入库单（事务：按明细冲减库存 → 删除明细与主表，物理删除）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);
}
