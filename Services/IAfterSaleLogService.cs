using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 售后日志 服务接口（trx_refund_log 查询）
/// </summary>
public interface IAfterSaleLogService
{
    /// <summary>查询某退款单的全链路操作日志</summary>
    Task<ApiResponse<List<RefundLogDto>>> GetByRefundAsync(long refundId);

    /// <summary>查询某退货单的操作日志（通过退货单关联的退款单查询）</summary>
    Task<ApiResponse<List<RefundLogDto>>> GetByReturnAsync(long returnId);
}
