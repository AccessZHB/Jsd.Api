using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 收款记录 仓储接口
/// </summary>
public interface IPaymentRepository : IRepository<MktPayment>
{
    /// <summary>分页查询收款记录（关联补全客户名称）</summary>
    Task<(List<PaymentRow> Items, int Total)> GetPagedListAsync(
        long? memberId, string? memberName, string? paymentNo, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>带跟踪查询（核销时要改 status）</summary>
    Task<MktPayment?> GetByIdTrackedAsync(long id);

    /// <summary>按收款单号查询（查重用）</summary>
    Task<MktPayment?> GetByPaymentNoAsync(string paymentNo);

    /// <summary>查询某收款单的核销明细（联查订单号，便于前端展示核销到哪张订单）</summary>
    Task<List<PaymentItemRow>> GetWriteOffItemsAsync(long paymentId);

    /// <summary>生成唯一收款单号：PAY + yyyyMMddHHmmss + 4位随机数</summary>
    Task<string> GeneratePaymentNoAsync();

    /// <summary>批量新增核销明细</summary>
    Task AddItemsAsync(List<MktPaymentItem> items);

    // ---------- 支付日志 trx_payment_log（内部对账表，无独立菜单） ----------

    /// <summary>按订单ID查询支付日志（按回调时间倒序；无回调时间时用创建时间兜底）</summary>
    Task<List<TrxPaymentLog>> GetLogsByOrderIdAsync(long orderId);

    /// <summary>按商户订单号(即 trx_order.order_no)查询支付日志</summary>
    Task<TrxPaymentLog?> GetLogByOutTradeNoAsync(string outTradeNo);

    /// <summary>
    /// 【幂等核心】按微信支付交易号(transaction_id)查询支付日志。
    /// 微信可能因网络抖动重复推送同一笔回调，必须先按交易号判重，
    /// 已成功处理过的直接返回，避免重复更新订单导致账目翻倍。
    /// </summary>
    Task<TrxPaymentLog?> GetLogByTransactionIdAsync(string transactionId);

    /// <summary>同上，但【带跟踪】：同一笔回调状态变化（失败→成功）时需要用它拿到可更新实体</summary>
    Task<TrxPaymentLog?> GetLogByTransactionIdTrackedAsync(string transactionId);

    /// <summary>按商户订单号查询，【带跟踪】</summary>
    Task<TrxPaymentLog?> GetLogByOutTradeNoTrackedAsync(string outTradeNo);

    /// <summary>新增支付日志</summary>
    Task AddLogAsync(TrxPaymentLog log);
}

/// <summary>收款记录列表行（实体 + 客户名称）</summary>
public class PaymentRow
{
    /// <summary>收款单实体</summary>
    public MktPayment Payment { get; set; } = null!;

    /// <summary>客户名称（mem_member.name）</summary>
    public string MemberName { get; set; } = string.Empty;
}

/// <summary>核销明细行（实体 + 对应订单号）</summary>
public class PaymentItemRow
{
    /// <summary>核销明细实体</summary>
    public MktPaymentItem Item { get; set; } = null!;

    /// <summary>被核销应收单对应的订单号（经 trx_receivable → trx_order）</summary>
    public string OrderNo { get; set; } = string.Empty;
}
