namespace Jsd.Api.Models.Payment;

/// <summary>微信支付 V3 解密后的交易对象（回调明文 / 查单响应 同结构）</summary>
public class WeChatPayTransactionDto
{
    /// <summary>商户订单号</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信支付交易号（支付成功后才有）</summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// 交易状态：SUCCESS（支付成功）/ REFUND（转入退款）/ NOTPAY（未支付）/
    /// CLOSED（已关闭）/ REVOKED（已撤销）/ USERPAYING（支付中）/ PAYERROR（支付失败）
    /// </summary>
    public string TradeState { get; set; } = string.Empty;

    /// <summary>交易状态描述</summary>
    public string? TradeStateDesc { get; set; }

    /// <summary>订单金额（单位：分），来自 amount.total</summary>
    public int Total { get; set; }

    /// <summary>支付完成时间（格式 yyyy-MM-ddTHH:mm:ss+08:00）</summary>
    public string? SuccessTime { get; set; }
}

/// <summary>回调处理结果（供控制器转成微信期望的 {code,message}）</summary>
public class PayNotifyResultDto
{
    /// <summary>支付单ID</summary>
    public long PaymentOrderId { get; set; }

    /// <summary>商户订单号</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信交易号</summary>
    public string? TransactionId { get; set; }

    /// <summary>最终本地支付状态（见 PayOrderStatus）</summary>
    public int Status { get; set; }

    /// <summary>是否幂等命中（true=此前已处理，本次未写库）</summary>
    public bool Duplicated { get; set; }

    /// <summary>处理描述</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>主动查单/补单结果</summary>
public class PayQueryResultDto
{
    /// <summary>商户订单号</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信交易号（未支付为空）</summary>
    public string? TransactionId { get; set; }

    /// <summary>微信返回的交易状态（SUCCESS/NOTPAY/CLOSED…）</summary>
    public string TradeState { get; set; } = string.Empty;

    /// <summary>本地最终支付状态（见 PayOrderStatus）</summary>
    public int Status { get; set; }

    /// <summary>本次执行动作：SUCCESS（补单成功）/ CLOSED（超时关单）/ UNCHANGED（仍支付中，未变动）</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>说明</summary>
    public string Message { get; set; } = string.Empty;
}
