using System.Text.RegularExpressions;

namespace Jsd.Api.Services;

/// <summary>
/// User-Agent 轻量解析（浏览器类型 / 操作系统）。
/// 不引第三方包，识别不出就返回原文截断值 —— 文档九-10：解析降级，不阻塞主流程。
/// </summary>
public static class UserAgentParser
{
    /// <summary>解析浏览器类型</summary>
    public static string ParseBrowser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return string.Empty;
        var ua = userAgent;

        // 顺序有讲究：先匹配"套壳内核"再匹配通用内核（Edge/360 等都含 Chrome 字样）
        if (ua.Contains("Edg/")) return "Edge";
        if (ua.Contains("QQBrowser")) return "QQ浏览器";
        if (ua.Contains("MicroMessenger")) return "微信内置";
        if (ua.Contains("Firefox")) return "Firefox";
        if (ua.Contains("Chrome")) return "Chrome";
        if (ua.Contains("Safari") && !ua.Contains("Chrome")) return "Safari";
        if (ua.Contains("MSIE") || ua.Contains("Trident/")) return "IE";

        return Truncate(ua, 50);
    }

    /// <summary>解析操作系统</summary>
    public static string ParseOs(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return string.Empty;
        var ua = userAgent;

        if (ua.Contains("Windows NT 10") || ua.Contains("Windows NT 11")) return "Windows 10/11";
        if (ua.Contains("Windows NT 6.1")) return "Windows 7";
        if (ua.Contains("Windows")) return "Windows";
        if (ua.Contains("Mac OS X") || ua.Contains("Macintosh")) return "macOS";
        if (ua.Contains("Android")) return "Android";
        if (ua.Contains("iPhone") || ua.Contains("iPad")) return "iOS";
        if (ua.Contains("Linux")) return "Linux";

        return Truncate(ua, 50);
    }

    /// <summary>超长截断（数据库列宽保护）</summary>
    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value.Substring(0, maxLength);
}
