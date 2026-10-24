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

    /// <summary>
    /// 会员自助申请售后（小程序端 POST /api/mp/after-sales/apply）。
    /// 与后台 CreateAsync 的差异：允许「已支付(1)/已发货(2)/已完成(3)」订单申请；
    /// 强制校验订单归属当前会员；创建后把订单置为 5-售后中。
    /// </summary>
    Task<ApiResponse<RefundCreateResult>> ApplyByMemberAsync(MpApplyRefundDto dto, long memberId);

    /// <summary>
    /// 会员售后列表（小程序端 GET /api/mp/after-sales/list）。
    /// 仅返回该会员本人的售后单，附带订单商品摘要与小程序口径的状态文案。
    /// </summary>
    Task<ApiResponse<PagedResult<MpRefundListItemDto>>> GetMyListAsync(long memberId, int page, int pageSize);
}
