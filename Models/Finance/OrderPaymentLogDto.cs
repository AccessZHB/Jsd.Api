namespace Jsd.Api.Models.Finance;

/// <summary>
/// 订单支付回调日志 DTO（订单详情页"支付记录"时间轴专用）
///
/// 【用途】排查"掉单"问题：看微信到底有没有回调、回调了几次、每次结果是什么。
/// 【数据来源】trx_payment_log，只读，不做任何聚合加工。
///
/// 【字段映射】（本 Web API 统一 camelCase 序列化，下表与需求文档字段名一一对应）
///   需求字段                    C# 属性                 JSON 输出
///   wechat_transaction_id   →   WechatTransactionId  →  wechatTransactionId
///   amount                  →   Amount               →  amount
///   notify_time             →   NotifyTime           →  notifyTime
///   status                  →   Status + StatusText  →  status / statusText
///   raw_xml                 →   RawXml               →  rawXml
/// </summary>
public class OrderPaymentLogDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>关联订单ID</summary>
    public long OrderId { get; set; }

    /// <summary>订单号（冗余，便于核对）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>微信支付交易号（wechat_transaction_id；回调未带时为空）</summary>
    public string? WechatTransactionId { get; set; }

    /// <summary>商户订单号（out_trade_no）</summary>
    public string? OutTradeNo { get; set; }

    /// <summary>支付金额（元）</summary>
    public decimal Amount { get; set; }

    /// <summary>微信回调时间（notify_time）</summary>
    public DateTime? NotifyTime { get; set; }

    /// <summary>支付状态：0-待支付 1-已支付 2-已取消 3-已退款 4-支付失败</summary>
    public int Status { get; set; }

    /// <summary>是否成功（status = 1 已支付）</summary>
    public bool Success { get; set; }

    /// <summary>状态文本：成功 / 失败 / 待支付 / 已取消 / 已退款（供前端时间轴标题直接展示）</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// 回调原始报文（raw_xml）。
    /// 支付失败时前端提供"复制报文"按钮，开发人员可直接拿去微信后台调试。
    /// </summary>
    public string? RawXml { get; set; }

    /// <summary>错误码（支付失败时）</summary>
    public string? ErrorCode { get; set; }

    /// <summary>错误描述（支付失败时）</summary>
    public string? ErrorMsg { get; set; }

    /// <summary>记录创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 是否重复回调（同一 transaction_id 在库中已存在过多条成功记录时为 true）。
    /// 用于排查"微信重复推送"场景，正常应为 false。
    /// </summary>
    public bool IsDuplicate { get; set; }
}
