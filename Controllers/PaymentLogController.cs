using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 支付日志查询接口（读接口，供订单详情页"支付记录"时间轴排查掉单用）
///
/// trx_payment_log 是内部日志表，【不需要独立菜单】，
/// 只保留读写接口：写入由 PaymentCallbackController（微信回调）负责，
/// 这里只提供按订单维度的查询。
///
/// 权限：跟随订单查看权限 order:list（支付记录属于订单详情的一部分）。
/// </summary>
[ApiController]
[Route("api/finance/payment-log")]
[Authorize]
public class PaymentLogController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentLogController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// 查询某订单的微信支付回调日志。
    ///
    /// 示例：GET /api/finance/payment-log/by-order/1001
    ///
    /// 返回字段：wechatTransactionId / amount / notifyTime / status / statusText /
    ///          rawXml（原始报文，失败时可复制去微信后台调试）/ errorCode / errorMsg
    /// 排序：按【回调时间倒序】，最新的一次排在最上面。
    /// </summary>
    [HttpGet("by-order/{orderId}")]
    public async Task<ApiResponse<List<OrderPaymentLogDto>>> GetByOrder(long orderId)
    {
        if (orderId <= 0)
        {
            return ApiResponse<List<OrderPaymentLogDto>>.Fail("订单ID不合法");
        }

        var list = await _paymentService.GetOrderPaymentLogsAsync(orderId);
        return ApiResponse<List<OrderPaymentLogDto>>.Success(list);
    }
}
