using Jsd.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace Jsd.Api.Attributes;

/// <summary>
/// 【阶段五】接口防重放特性（基于 IMemoryCache，只作用于标注它的高危接口）。
///
/// 客户端必须携带两个请求头：
///   - <c>X-Nonce</c>：随机字符串（每次请求都不同，建议 UUID/GUID）
///   - <c>X-Timestamp</c>：Unix 时间戳（秒或毫秒均可，13 位按毫秒处理）
///
/// 校验顺序（任一失败即拦截，不进入业务逻辑）：
///   1. 两个头缺失 → 400；
///   2. |服务器UTC时间 - X-Timestamp| &gt; 60 秒 → 403「请求已过期」（防延迟重放）；
///   3. 缓存中存在 `replay:{nonce}` → 403「重复请求」（防同包重放）；
///   4. 校验通过 → **立即**写入 `replay:{nonce}`（TTL 2 分钟）→ 放行。
///
/// 为什么只标在 Login / 支付类接口：
///   防重放需要客户端配合加头，且会多一次缓存读写。若做成全局过滤器，
///   所有 GET 列表接口都会被牵连（性能与兼容性双输）。
///   本特性可自由标注在任意 Action 或 Controller 上，按需开启。
///
/// ⚠️ 多实例部署提示：Nonce 存在进程内 IMemoryCache，多实例下各自独立。
///    攻击者若被负载均衡打到不同实例，同一 Nonce 可能各过一次。
///    若将来上集群，需要把 Nonce 存储换成共享存储（如 Redis）。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class ReplayAttackAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>允许的时间偏差（秒），默认 60 秒</summary>
    public int MaxSkewSeconds { get; set; } = 60;

    /// <summary>Nonce 记忆时长（秒），默认 120 秒。必须 ≥ 2 × MaxSkewSeconds，
    /// 否则过期后同一 Nonce 又能在新的时间窗内复用。</summary>
    public int NonceTtlSeconds { get; set; } = 120;

    private const string NonceHeader = "X-Nonce";
    private const string TimestampHeader = "X-Timestamp";
    private const string CacheKeyPrefix = "replay:";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var nonce = http.Request.Headers[NonceHeader].ToString();
        var timestampRaw = http.Request.Headers[TimestampHeader].ToString();

        // ---- 1) 参数缺失 ----
        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(timestampRaw))
        {
            context.Result = Reject(StatusCodes.Status400BadRequest,
                $"缺少防重放请求头（{NonceHeader} / {TimestampHeader}）");
            return;
        }

        if (nonce.Length > 128)
        {
            context.Result = Reject(StatusCodes.Status400BadRequest, $"{NonceHeader} 长度超限");
            return;
        }

        // ---- 2) 时间窗校验（防延迟重放）----
        if (!long.TryParse(timestampRaw, out var tsRaw) || tsRaw <= 0)
        {
            context.Result = Reject(StatusCodes.Status400BadRequest, $"{TimestampHeader} 格式非法（需 Unix 时间戳）");
            return;
        }

        // 兼容秒（10 位）与毫秒（13 位）
        var tsSeconds = timestampRaw.Length >= 13 ? tsRaw / 1000 : tsRaw;
        var requestTime = DateTimeOffset.FromUnixTimeSeconds(tsSeconds);
        var skewSeconds = Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalSeconds);

        if (skewSeconds > MaxSkewSeconds)
        {
            context.Result = Reject(StatusCodes.Status403Forbidden,
                $"请求已过期（时间偏差 {skewSeconds:0} 秒，允许 ±{MaxSkewSeconds} 秒）");
            return;
        }

        // ---- 3) Nonce 去重（防同包重放）----
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = CacheKeyPrefix + nonce;

        if (cache.TryGetValue(cacheKey, out _))
        {
            context.Result = Reject(StatusCodes.Status403Forbidden, "重复请求（疑似重放攻击），已拦截");
            return;
        }

        // ---- 4) 先占位再执行：即使业务失败，这次 Nonce 也作废，避免被反复重放 ----
        cache.Set(cacheKey, 1, TimeSpan.FromSeconds(NonceTtlSeconds));

        await next();
    }

    /// <summary>统一返回：HTTP 403/400 + ApiResponse 结构，便于前端与 Postman 直接读 message</summary>
    private static JsonResult Reject(int statusCode, string message) => new(ApiResponse<object>.Fail(message, statusCode))
    {
        StatusCode = statusCode
    };
}
