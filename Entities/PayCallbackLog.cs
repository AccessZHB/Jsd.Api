using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 微信支付回调日志表 pay_callback_log（微信支付主动查单与补单模块）
///
/// 每次微信回调都原样落库请求体 JSON，便于排查「掉单 / 重复推送 / 验签失败」等问题。
/// 与 payment_order 的关系：一条支付单可能对应多条回调日志（微信会重试推送）。
/// </summary>
[Table("pay_callback_log")]
public class PayCallbackLog
{
    /// <summary>记录ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>回调请求体原始 JSON（排查问题用，TEXT 足够）</summary>
    [Column("request_body")]
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>微信订单号（解密/解析后；验签失败或解析失败则为空）</summary>
    [StringLength(64)]
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    /// <summary>处理状态（见 CallbackHandleStatus：0-待处理 1-成功 2-失败 3-已忽略）</summary>
    [Column("handle_status")]
    public int HandleStatus { get; set; }

    /// <summary>响应结果（SUCCESS / 失败原因 / 幂等说明）</summary>
    [Column("response_result")]
    public string? ResponseResult { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
