using Jsd.Api.Models.Common;
using Jsd.Api.Models.Payment;

namespace Jsd.Api.Services;

/// <summary>微信支付 主动查单与补单 服务接口</summary>
public interface IPayService
{
    /// <summary>
    /// 处理微信支付结果通知（V3 回调）。
    /// 流程：读取原始请求体 → 验签 → AES-256-GCM 解密 → 幂等更新 payment_order。
    /// 已处理过的订单直接返回 SUCCESS（幂等），不重复推进业务。
    /// </summary>
    Task<ApiResponse<PayNotifyResultDto>> HandleNotifyCallbackAsync(HttpRequest request);

    /// <summary>
    /// 主动调用微信【查询订单 API】（V3），并同步更新本地 payment_order 状态。
    /// 微信回支付成功 → 本地更新 SUCCESS；微信回未支付且已超时 → 本地更新 CLOSED 关单。
    /// 供「补单」「定时任务」「后台手动查单」复用同一套逻辑。
    /// </summary>
    Task<ApiResponse<PayQueryResultDto>> QueryOrderFromWeChatAsync(string outTradeNo);

    /// <summary>
    /// 创建支付单（业务系统发起支付时调用：先落 payment_order(PAYING)，再拿 out_trade_no 去微信下单）。
    /// out_trade_no 由调用方保证唯一（本方法再靠数据库唯一索引兜底）。
    /// </summary>
    Task<ApiResponse<long>> CreatePaymentOrderAsync(long bizOrderId, string outTradeNo, long amount, int payChannel, DateTime expireTime);
}
