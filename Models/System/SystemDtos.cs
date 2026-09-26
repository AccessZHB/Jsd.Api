using System.Text.Json.Serialization;

namespace Jsd.Api.Models.System;

// ============================================================
// 分页查询入参公共契约（与价格模块 IPagedQuery 一致：Page/PageSize）
// 注意：需求文档写的是 pageNum/pageSize，项目实际契约是 page/pageSize，
// 前端 request 层已按 { list, total, page, pageSize } 解析 —— 以项目为准。
// ============================================================

/// <summary>分页查询入参公共契约（便于 Service 层统一归一化 Page / PageSize）</summary>
public interface ISystemPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    int Page { get; set; }

    /// <summary>每页条数（1~100）</summary>
    int PageSize { get; set; }
}

// ============================================================
// 一、操作日志 DTO（sys_operation_log）
// ============================================================

/// <summary>操作日志分页查询入参</summary>
public class OperationLogQueryDto : ISystemPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>操作人姓名（模糊）</summary>
    public string? OperatorName { get; set; }

    /// <summary>操作模块（模糊）</summary>
    public string? Module { get; set; }

    /// <summary>操作动作（模糊）</summary>
    public string? Action { get; set; }

    /// <summary>操作状态：1-成功 0-失败（空=全部）</summary>
    public int? Status { get; set; }

    /// <summary>开始时间（含）</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间（含）</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>操作日志出参（列表用，不含大字段）</summary>
public class OperationLogDto
{
    public long Id { get; set; }

    public long? OperatorId { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string RequestMethod { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    /// <summary>操作IP地址</summary>
    public string IpAddress { get; set; } = string.Empty;

    public string Browser { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    /// <summary>接口执行耗时（毫秒）</summary>
    public int ExecutionTime { get; set; }

    /// <summary>操作状态：1-成功 0-失败</summary>
    public int Status { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>操作日志详情出参（含请求参数 / 响应结果 / 异常堆栈大字段）</summary>
public class OperationLogDetailDto : OperationLogDto
{
    /// <summary>请求参数（JSON，已脱敏）</summary>
    public string? RequestParams { get; set; }

    /// <summary>响应结果（JSON，已脱敏）</summary>
    public string? ResponseResult { get; set; }

    /// <summary>客户端User-Agent</summary>
    public string? UserAgent { get; set; }

    /// <summary>异常堆栈信息（成功时为空）</summary>
    public string? ErrorMessage { get; set; }
}

// ============================================================
// 二、登录日志 DTO（sys_login_log）
// ============================================================

/// <summary>登录日志分页查询入参</summary>
public class LoginLogQueryDto : ISystemPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>管理员用户名（模糊）</summary>
    public string? MemberName { get; set; }

    /// <summary>登录IP（模糊）</summary>
    public string? IpAddress { get; set; }

    /// <summary>登录方式：1-账号密码 2-微信授权 3-短信验证码（空=全部）</summary>
    public int? LoginType { get; set; }

    /// <summary>登录状态：1-成功 0-失败（空=全部）</summary>
    public int? Status { get; set; }

    /// <summary>开始时间（含）</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间（含）</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>登录日志出参</summary>
public class LoginLogDto
{
    public long Id { get; set; }

    public long? MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    /// <summary>登录方式：1-账号密码 2-微信授权 3-短信验证码</summary>
    public int LoginType { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string Browser { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    public string LoginLocation { get; set; } = string.Empty;

    /// <summary>登录状态：1-成功 0-失败</summary>
    public int Status { get; set; }

    public string FailReason { get; set; } = string.Empty;

    public DateTime? LoginTime { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>已锁定账号查询入参</summary>
public class LockAccountQueryDto : ISystemPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>账号（模糊）</summary>
    public string? MemberName { get; set; }

    /// <summary>最后登录IP（模糊）</summary>
    public string? IpAddress { get; set; }
}

/// <summary>已锁定账号出参</summary>
public class LockAccountDto
{
    public long MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    /// <summary>连续失败次数</summary>
    public int FailCount { get; set; }

    /// <summary>锁定截止时间</summary>
    public DateTime? LockUntil { get; set; }

    /// <summary>锁定开始时间（sys_user 未单独存，由 截止 - 锁定时长 推算；sys_user 无该列）</summary>
    public DateTime? LockStartTime { get; set; }

    /// <summary>触发锁定的登录方式：1-账号密码 2-微信授权 3-短信验证码（取该账号最近一条失败登录日志）</summary>
    public int? LoginType { get; set; }

    /// <summary>最后登录IP</summary>
    public string? LastLoginIp { get; set; }

    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginTime { get; set; }
}

// ============================================================
// 五、日志统计概览（前端页面顶部统计卡片）
// ============================================================

/// <summary>操作日志统计概览（今日）</summary>
public class OperationLogStatisticsDto
{
    /// <summary>今日操作总数</summary>
    public int TodayTotal { get; set; }

    /// <summary>今日成功数（status=1）</summary>
    public int TodaySuccess { get; set; }

    /// <summary>今日失败数（status=0）</summary>
    public int TodayFail { get; set; }

    /// <summary>今日平均耗时（毫秒，保留 1 位小数）</summary>
    public double AvgExecutionTime { get; set; }
}

/// <summary>登录日志统计概览（今日 + 当前锁定账号数）</summary>
public class LoginLogStatisticsDto
{
    /// <summary>今日登录总数</summary>
    public int TodayTotal { get; set; }

    /// <summary>今日成功数（status=1）</summary>
    public int TodaySuccess { get; set; }

    /// <summary>今日失败数（status=0）</summary>
    public int TodayFail { get; set; }

    /// <summary>当前仍处锁定状态的账号数</summary>
    public int LockedCount { get; set; }
}

// ============================================================
// 三、删除 / 清理公共入参
// ============================================================

/// <summary>批量删除入参</summary>
public class BatchDeleteDto
{
    /// <summary>要删除的日志ID列表</summary>
    public List<long> Ids { get; set; } = new();
}

/// <summary>清理过期日志入参</summary>
public class CleanLogDto
{
    /// <summary>保留天数（删除该天数之前的记录；操作日志默认 90，登录日志默认 180）</summary>
    public int Days { get; set; } = 90;
}

// ============================================================
// 四、登录安全策略 DTO
// ============================================================

/// <summary>登录安全策略配置出参/入参（存 sys_config 的 security.login 键）</summary>
public class SecurityConfigDto
{
    /// <summary>连续登录失败锁定阈值（次）</summary>
    [JsonPropertyName("maxLoginAttempts")]
    public int MaxLoginAttempts { get; set; } = 5;

    /// <summary>锁定时长（分钟）</summary>
    [JsonPropertyName("lockDurationMinutes")]
    public int LockDurationMinutes { get; set; } = 30;

    /// <summary>密码最小长度</summary>
    [JsonPropertyName("passwordMinLength")]
    public int PasswordMinLength { get; set; } = 8;

    /// <summary>密码必须包含大写字母</summary>
    [JsonPropertyName("passwordRequireUpper")]
    public bool PasswordRequireUpper { get; set; } = true;

    /// <summary>密码必须包含小写字母</summary>
    [JsonPropertyName("passwordRequireLower")]
    public bool PasswordRequireLower { get; set; } = true;

    /// <summary>密码必须包含数字</summary>
    [JsonPropertyName("passwordRequireDigit")]
    public bool PasswordRequireDigit { get; set; } = true;

    /// <summary>登录是否必须校验验证码</summary>
    [JsonPropertyName("captchaEnabled")]
    public bool CaptchaEnabled { get; set; } = true;

    /// <summary>验证码有效期（分钟）</summary>
    [JsonPropertyName("captchaExpireMinutes")]
    public int CaptchaExpireMinutes { get; set; } = 5;

    /// <summary>验证码位数</summary>
    [JsonPropertyName("captchaLength")]
    public int CaptchaLength { get; set; } = 4;

    /// <summary>达到阈值后是否自动解锁（关闭则由管理员手动解锁）</summary>
    [JsonPropertyName("autoUnlock")]
    public bool AutoUnlock { get; set; } = true;

    /// <summary>密码必须包含特殊字符</summary>
    [JsonPropertyName("passwordRequireSpecial")]
    public bool PasswordRequireSpecial { get; set; } = false;

    /// <summary>密码有效期（天，0 = 永不过期）</summary>
    [JsonPropertyName("passwordValidDays")]
    public int PasswordValidDays { get; set; } = 0;

    /// <summary>访问令牌有效期（小时）</summary>
    [JsonPropertyName("tokenExpireHours")]
    public int TokenExpireHours { get; set; } = 24;

    /// <summary>是否允许并发登录（关闭时新登录踢掉旧会话）</summary>
    [JsonPropertyName("allowConcurrentLogin")]
    public bool AllowConcurrentLogin { get; set; } = false;

    /// <summary>最大并发会话数（allowConcurrentLogin 开启时生效）</summary>
    [JsonPropertyName("maxConcurrentSessions")]
    public int MaxConcurrentSessions { get; set; } = 1;
}

/// <summary>修改密码入参</summary>
public class ChangePasswordDto
{
    /// <summary>旧密码</summary>
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>新密码（需满足强度要求）</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>确认新密码（需与 NewPassword 一致）</summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>刷新 Token 入参</summary>
public class RefreshTokenDto
{
    /// <summary>刷新令牌（登录时返回的 refreshToken）</summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>验证码出参</summary>
public class CaptchaResultDto
{
    /// <summary>验证码会话ID（登录时回传 captchaKey）</summary>
    public string CaptchaKey { get; set; } = string.Empty;

    /// <summary>验证码图片（Base64 data URI，img 标签直接可用）</summary>
    public string CaptchaImage { get; set; } = string.Empty;
}

/// <summary>Token 签发出参（access + refresh）</summary>
public class TokenResultDto
{
    /// <summary>访问令牌</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>刷新令牌（旧 refreshToken 使用后立即失效）</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（秒）</summary>
    public int ExpiresIn { get; set; }
}
