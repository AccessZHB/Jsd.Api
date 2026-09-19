using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 微信支付回调接口（内部调用，无前端页面）
///
/// 流程：解析回调内容 → 验签 → 写入 trx_payment_log → 更新 trx_order 状态。
/// trx_payment_log 是内部日志表，不需要独立菜单，但保留读写接口供支付回调与对账使用。
///
/// 【安全说明】
///   微信服务器回调不会携带本系统的 JWT，因此本控制器标记 [AllowAnonymous]。
///   生产环境务必接入真实验签（见 PaymentService.VerifySign），防止伪造回调。
/// </summary>
[ApiController]
[Route("api/payment-callback")]
[AllowAnonymous]
public class PaymentCallbackController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentCallbackController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// 微信支付结果回调
    ///
    /// 请求体（JSON）：
    /// {
    ///   "outTradeNo": "ORD202609151030120001",
    ///   "transactionId": "4200001234567890",
    ///   "resultCode": "SUCCESS",
    ///   "amount": 5980.00,
    ///   "sign": "xxx",
    ///   "rawData": "&lt;xml&gt;...&lt;/xml&gt;"
    /// }
    ///
    /// 说明：微信官方回调是 XML 格式，如需直接接收 XML，
    /// 可在 Program.cs 增加 XML 输入格式化器，或在本接口内读取 Request.Body 原始字符串后解析，
    /// 再转换成 WechatCallbackDto 调用 HandleWechatCallbackAsync。
    /// </summary>
    [HttpPost("wechat")]
    public async Task<ApiResponse<object>> Wechat([FromBody] WechatCallbackDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.OutTradeNo))
        {
            return ApiResponse<object>.Fail("回调参数无效：缺少商户订单号");
        }

        try
        {
            var result = await _paymentService.HandleWechatCallbackAsync(dto);

            // Duplicated=true 表示同一微信交易号此前已成功处理过，本次被幂等拦截（未做任何写入）
            return ApiResponse<object>.Success(
                new { logId = result.LogId, duplicated = result.Duplicated },
                result.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
