using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;

namespace Jsd.Api.Services;

/// <summary>
/// 应付账款（采购付款）服务接口。
/// 应付款由入库确认时自动生成；本接口负责查询与线下付款核销。
/// 与财务模块的 mkt_payment（客户应收）对称：这里管"我们欠供应商的钱"。
/// </summary>
public interface IPayableService
{
    /// <summary>分页查询应付账款列表</summary>
    Task<ApiResponse<PagedResult<PayableListDto>>> GetPagedListAsync(PayableQueryDto query);

    /// <summary>应付账款详情（含关联采购订单号）</summary>
    Task<ApiResponse<PayableDetailDto>> GetDetailAsync(long id);

    /// <summary>按供应商查询应付账款</summary>
    Task<ApiResponse<List<PayableListDto>>> GetBySupplierAsync(long supplierId, int? status);

    /// <summary>付款核销：paid += 金额，unpaid -= 金额，状态 0/1/2</summary>
    Task<ApiResponse<object>> PayAsync(long id, PayPaymentDto dto);
}
