using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;

namespace Jsd.Api.Services;

/// <summary>采购订单服务接口</summary>
public interface IPurchaseOrderService
{
    /// <summary>分页查询采购订单列表</summary>
    Task<ApiResponse<PagedResult<PurchaseOrderListDto>>> GetPagedListAsync(PurchaseOrderQueryDto query);

    /// <summary>采购订单详情（含明细、已入库数量、入库记录）</summary>
    Task<ApiResponse<PurchaseOrderDetailDto>> GetDetailAsync(long id);

    /// <summary>创建采购订单（草稿）</summary>
    Task<ApiResponse<PurchaseOrderCreateResult>> CreateAsync(CreatePurchaseOrderDto dto);

    /// <summary>更新采购订单（仅草稿可编辑）</summary>
    Task<ApiResponse<object>> UpdateAsync(long id, UpdatePurchaseOrderDto dto);

    /// <summary>作废采购订单（仅草稿可作废）</summary>
    Task<ApiResponse<object>> CancelAsync(long id);

    /// <summary>
    /// 确认采购订单（草稿 → 待入库）。
    /// 本模块【无审核环节】：确认后订单即可开入库单，不记录审批人/审批时间。
    /// </summary>
    Task<ApiResponse<object>> ConfirmAsync(long id);

    /// <summary>按供应商查询订单列表（简版，不分页）</summary>
    Task<ApiResponse<List<PurchaseOrderListDto>>> GetBySupplierAsync(long supplierId, int? status);

    /// <summary>根据库存预警自动生成采购订单（按供应商分组）</summary>
    Task<ApiResponse<List<PurchaseOrderCreateResult>>> AutoCreateAsync(AutoCreateOrderDto dto);
}
