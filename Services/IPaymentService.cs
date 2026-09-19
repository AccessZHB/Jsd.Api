using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;

namespace Jsd.Api.Services;

/// <summary>
/// 收款与核销 服务接口（对应菜单 322：/finance/payment）
/// </summary>
public interface IPaymentService
{
    /// <summary>分页查询收款记录列表</summary>
    Task<PagedResult<PaymentListDto>> GetListAsync(PaymentPageQuery query);

    /// <summary>收款单详情（含核销明细）</summary>
    Task<PaymentDetailDto?> GetDetailAsync(long id);

    /// <summary>
    /// 录入线下收款单（插入 mkt_payment），状态为"待核销"。
    /// 收款单号由后端自动生成：PAY + yyyyMMddHHmmss + 4位随机数。
    /// </summary>
    Task<PaymentCreateResultDto> CreatePaymentAsync(PaymentCreateDto dto, int operatorId);

    /// <summary>
    /// 【核心】核销：把一笔收款拆分挂到多张应收账款上。
    /// 全流程在一个数据库事务内完成，任何一步失败整体回滚。
    /// </summary>
    Task WriteOffAsync(WriteOffInputDto dto);

    // ==================== 支付日志（trx_payment_log，内部对账用） ====================

    /// <summary>查询某订单的支付日志</summary>
    Task<List<PaymentLogDto>> GetLogsByOrderIdAsync(long orderId);

    /// <summary>
    /// 查询某订单的【支付回调日志】（订单详情页"支付记录"时间轴专用）。
    /// 返回 wechat_transaction_id / amount / notify_time / status / raw_xml 等排障字段，
    /// 按回调时间倒序（最新在最上）。
    /// </summary>
    Task<List<OrderPaymentLogDto>> GetOrderPaymentLogsAsync(long orderId);

    /// <summary>
    /// 处理微信支付回调：写入 trx_payment_log，并在支付成功时更新 trx_order 状态。
    ///
    /// 【幂等】同一 wechat_transaction_id 已成功处理过时直接返回 Duplicated=true，
    /// 不做任何写入、不推进订单、不改动金额。
    /// </summary>
    Task<WechatCallbackResultDto> HandleWechatCallbackAsync(WechatCallbackDto dto);
}
