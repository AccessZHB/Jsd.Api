using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 退款申请服务接口（售后模块）
/// </summary>
public interface IRefundService
{
    /// <summary>提交退款申请</summary>
    Task<ApiResponse<RefundCreateResult>> CreateAsync(CreateRefundDto dto);

    /// <summary>退款列表分页查询</summary>
    Task<ApiResponse<PagedResult<RefundListDto>>> GetPagedListAsync(RefundQueryDto q);

    /// <summary>退款详情（含关联退货单与全链路日志）</summary>
    Task<ApiResponse<RefundDetailDto>> GetDetailAsync(long id);

    /// <summary>审核退款（通过/驳回）</summary>
    Task<ApiResponse<object>> ApproveAsync(long id, ApproveRefundDto dto);

    /// <summary>执行退款（财务打款，幂等；退货退款需仓库先收货）</summary>
    Task<ApiResponse<object>> ExecuteAsync(long id);

    /// <summary>
    /// 执行退款核心逻辑（修改实体 + 写日志，不落库）。
    /// 供 ReturnService 在"仓库收货后自动触发退款"时复用，保证跨服务操作在同一事务内一次性提交。
    /// </summary>
    Task ExecuteRefundCoreAsync(TrxRefund refund, long operatorId, string operatorName);
}
