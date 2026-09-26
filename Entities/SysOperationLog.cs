using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 操作审计日志表 sys_operation_log
/// 记录后台管理员的所有写操作（POST/PUT/DELETE/PATCH），由 OperationLogFilter 异步采集写入。
/// 表结构以 Jsd_order.sql（约 1982 行）为准。
/// </summary>
[Table("sys_operation_log")]
public class SysOperationLog
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>操作人ID，关联 sys_user 表</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>操作人姓名</summary>
    [StringLength(50)]
    [Column("operator_name")]
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>操作模块（如：商品管理、订单管理）</summary>
    [StringLength(50)]
    [Column("module")]
    public string Module { get; set; } = string.Empty;

    /// <summary>操作动作（如：新增商品、导出订单）</summary>
    [StringLength(100)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>HTTP请求方法（GET/POST/PUT/DELETE）</summary>
    [StringLength(10)]
    [Column("request_method")]
    public string RequestMethod { get; set; } = string.Empty;

    /// <summary>请求接口路径</summary>
    [StringLength(255)]
    [Column("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>请求参数（JSON格式，敏感字段脱敏）</summary>
    [Column("request_params")]
    public string? RequestParams { get; set; }

    /// <summary>响应结果（JSON格式，脱敏）</summary>
    [Column("response_result")]
    public string? ResponseResult { get; set; }

    /// <summary>操作IP地址</summary>
    [StringLength(50)]
    [Column("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>客户端User-Agent</summary>
    [StringLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>浏览器类型（自动解析）</summary>
    [StringLength(50)]
    [Column("browser")]
    public string Browser { get; set; } = string.Empty;

    /// <summary>操作系统（自动解析）</summary>
    [StringLength(50)]
    [Column("os")]
    public string Os { get; set; } = string.Empty;

    /// <summary>接口执行耗时（毫秒）</summary>
    [Column("execution_time")]
    public int ExecutionTime { get; set; }

    /// <summary>操作状态：1-成功 0-失败</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>异常堆栈信息（成功时为空）</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>操作时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
