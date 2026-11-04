namespace Jsd.Api.Helpers;

/// <summary>
/// 【阶段五】时区一致性守卫。
///
/// 背景：本项目所有 `DATETIME` 列**存的是服务器本地时间**，业务代码统一用 `DateTime.Now`
/// （仅 JWT 用 `DateTime.UtcNow`，这是标准的、正确的——JWT 的 exp 本身就是 UTC 语义）。
/// 真正的风险不是"用了本地时间"，而是：**部署到 TZ 不是东八区的机器上时，同一份代码算出不同结果**。
/// 典型后果：
///   - 报表按天分组偏移 8 小时（00:00~08:00 的订单被算到前一天）；
///   - 对账任务 `DateTime.Today.AddDays(-1)` 取错账期；
///   - 登录锁定 15 分钟变成 8 小时 15 分（若写入与比较用了不同基准）。
///
/// 因此在**启动时显式校验**，把问题挡在部署阶段，而不是等线上跑出脏数据。
/// 只告警不阻断启动：时区配置错误不应让整站起不来，但必须留下 Critical 日志。
/// </summary>
public static class TimeZoneGuard
{
    /// <summary>本项目约定的时区偏移（东八区）</summary>
    private static readonly TimeSpan ExpectedOffset = TimeSpan.FromHours(8);

    /// <summary>
    /// 校验服务器时区与本项目的时间约定是否一致。
    /// 配置 `Time:WarnOnTimeZoneMismatch=false` 可关闭（仅在明确要跑 UTC 环境且已完成迁移时使用）。
    /// </summary>
    public static void Verify(ILogger? logger, IConfiguration configuration)
    {
        var enabled = configuration.GetValue("Time:WarnOnTimeZoneMismatch", true);
        if (!enabled) return;

        var local = TimeZoneInfo.Local;
        var offset = local.GetUtcOffset(DateTime.UtcNow);

        if (offset == ExpectedOffset)
        {
            logger?.LogInformation("时区校验通过：{TimeZone}（UTC{Offset}），与项目时间约定一致",
                local.Id, FormatOffset(offset));
            return;
        }

        logger?.LogCritical(
            "【时区不一致】当前服务器时区为 {TimeZone}（UTC{Offset}），而本项目所有 DATETIME 列与业务代码均按东八区写入。" +
            "在此机器上运行会导致：报表按天分组偏移、对账日切取错账期、任何依赖本地时间的区间查询结果错误。" +
            "请把服务器时区设为 Asia/Shanghai（Windows 为 China Standard Time），" +
            "或先完成《时区统一迁移方案》再部署到 UTC 环境。",
            local.Id, FormatOffset(offset));
    }

    private static string FormatOffset(TimeSpan offset)
        => $"{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";
}
