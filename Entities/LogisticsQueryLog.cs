using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 物流轨迹查询日志表 logistics_query_log
///
/// 每次「缓存未命中」的真实第三方查询都会落一条记录，用于：
///   1) 排查第三方物流聚合平台（快递100/快递鸟）的异常返回与计费核对；
///   2) 回溯某单号某次查询到的原始 JSON（query_result）。
///
/// 与需求文档一致，但列名以本项目 MySQL 规范对齐（snake_case + utf8mb4）。
/// 逻辑：成功 status=1，失败 status=0（error_message 记录原因）。
/// </summary>
[Table("logistics_query_log")]
public class LogisticsQueryLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联订单ID（来自订单详情的 orderId 透传；无则 0）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>快递单号</summary>
    [Required]
    [StringLength(50)]
    [Column("tracking_number")]
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>快递公司编码（如 shunfeng / yuantong；智能识别失败时为空）</summary>
    [StringLength(50)]
    [Column("carrier_code")]
    public string CarrierCode { get; set; } = string.Empty;

    /// <summary>快递公司名称</summary>
    [StringLength(100)]
    [Column("carrier_name")]
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>完整响应JSON（排查问题用；TEXT 长文本）</summary>
    [Column("query_result")]
    public string QueryResult { get; set; } = string.Empty;

    /// <summary>查询状态：1-成功 0-失败</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>错误信息（失败时填充）</summary>
    [StringLength(500)]
    [Column("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>查询时间（数据库默认值 CURRENT_TIMESTAMP，也可由代码写入）</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
