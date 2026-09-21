using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 退货管理 服务接口（售后模块）
/// </summary>
public interface IReturnService
{
    /// <summary>创建退货单（手动创建或退款审核自动生成）</summary>
    Task<ApiResponse<ReturnCreateResult>> CreateAsync(CreateReturnDto dto);

    /// <summary>退货列表分页查询</summary>
    Task<ApiResponse<PagedResult<ReturnListDto>>> GetPagedListAsync(ReturnQueryDto q);

    /// <summary>退货详情（含明细）</summary>
    Task<ApiResponse<ReturnDetailDto>> GetDetailAsync(long id);

    /// <summary>仓库确认收货（库存回滚 + 自动触发退款）</summary>
    Task<ApiResponse<object>> ReceiveAsync(long id, ReceiveReturnDto dto);

    /// <summary>根据退款单查询关联退货单</summary>
    Task<ApiResponse<List<ReturnListDto>>> GetByRefundAsync(long refundId);
}
