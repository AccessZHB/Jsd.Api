using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockWarning;

namespace Jsd.Api.Services;

/// <summary>
/// 库存预警服务接口。
///
/// 接口清单（路由前缀 /api/stockWarning，与 stockIn / stockOut / stockCheck 一致采用 camelCase）：
///   GET    /api/stockWarning              分页查询（商品/状态/仅看预警 筛选 + 当前库存 + 是否触发预警）
///   GET    /api/stockWarning/{id}         单条详情（编辑回显）
///   POST   /api/stockWarning              新增预警配置
///   PUT    /api/stockWarning              编辑预警配置（id 在请求体）
///   PUT    /api/stockWarning/enable/{id}  启用/停用切换
///   DELETE /api/stockWarning/{id}         删除预警配置
/// </summary>
public interface IStockWarningService
{
    /// <summary>分页查询预警配置（多条件筛选 + 关联商品/SKU名称 + 当前库存 + 是否已触发预警）</summary>
    Task<ApiResponse<PagedResult<StockWarningItemDto>>> GetPagedListAsync(
        long? prodInfoId, int? status, bool? onlyWarning, int page, int pageSize);

    /// <summary>查询单条预警配置（编辑回显用）</summary>
    Task<ApiResponse<StockWarningItemDto>> GetDetailAsync(long id);

    /// <summary>新增 / 编辑预警配置（含参数校验；Id 为空或 0 为新增）</summary>
    Task<ApiResponse<object>> SaveAsync(StockWarningSaveDto dto);

    /// <summary>启用 / 停用切换（Status 不传则取反）</summary>
    Task<ApiResponse<object>> SetEnableAsync(long id, StockWarningEnableDto dto);

    /// <summary>删除预警配置（配置表无业务流水，直接物理删除）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);
}
