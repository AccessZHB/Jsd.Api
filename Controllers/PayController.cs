using Jsd.Api.Models.Common;
using Jsd.Api.Models.Payment;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 微信支付 主动查单与补单 接口
///
/// 路由说明（与既有 Finance 模块 PaymentCallbackController 区分）：
///   · 本控制器是【V3 标准实现】（真实验签 + AES 解密 + 主动查单），路由前缀 api/pay；
///   · 既有 api/payment-callback 是早期占位实现（无真实验签），待后续统一迁移到本模块。
///
/// 回调端点 [AllowAnonymous]：微信服务器不带本系统 JWT。
/// </summary>
[ApiController]
[Route("api/pay")]
public class PayController : ControllerBase
{
    private readonly IPayService _payService;

    public PayController(IPayService payService)
    {
        _payService = payService;
    }

    /// <summary>
    /// 微信支付结果通知（V3 回调）。
    /// 微信要求：HTTP 200 + 响应体 {"code":"SUCCESS","message":"成功"} 才视为成功；
    /// 非 200 微信会按策略重试。故本接口在成功时返回该标准结构，失败时返回 401/500 让微信重试。
    /// </summary>
    [HttpPost("notify")]
    [AllowAnonymous]
    public async Task<IActionResult> Notify()
    {
        var result = await _payService.HandleNotifyCallbackAsync(Request);

        if (result.Code == 200 && result.Data != null)
        {
            // 微信标准成功响应
            return Ok(new { code = "SUCCESS", message = "成功" });
        }

        // 验签失败 / 未配置 → 401；其它处理异常 → 500，微信都会重试
        var status = result.Code == 401 ? 401 : 500;
        return StatusCode(status, new { code = "FAIL", message = result.Message });
    }

    /// <summary>
    /// 后台手动主动查单 / 补单（运营排查掉单用）。需登录鉴权。
    /// GET /api/pay/query/{outTradeNo}
    /// </summary>
    [HttpGet("query/{outTradeNo}")]
    [Authorize]
    public async Task<ApiResponse<PayQueryResultDto>> Query([FromRoute] string outTradeNo)
    {
        try
        {
            return await _payService.QueryOrderFromWeChatAsync(outTradeNo);
        }
        catch (Exception ex)
        {
            return ApiResponse<PayQueryResultDto>.Fail($"查询失败：{ex.Message}");
        }
    }
}
