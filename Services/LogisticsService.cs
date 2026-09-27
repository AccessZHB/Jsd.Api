using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Logistics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jsd.Api.Services;

/// <summary>
/// 物流轨迹查询服务实现（物流轨迹查询模块）
///
/// 设计要点：
///   1) 第三方对接：默认对接【快递100】实时查询接口（poll/query.do），一次对接覆盖国内主流快递。
///      切换为快递鸟时只需改 _trackUrl 与签名算法（二者文档不同，已在方法内注释标注）。
///   2) 智能单号识别：快递100 支持 com=auto 自动识别承运商，故无需用户手动选择快递公司。
///   3) 缓存：进程内 IMemoryCache（本项目无 Redis，与字典/系统配置/验证码一致），
///      缓存 Key = logistics:track:{trackingNumber}，TTL 由配置 CacheMinutes（默认 60 分钟）。
///      多实例部署时把 IMemoryCache 换成 Redis 实现即可，本类一行都不用改。
///   4) 异常处理：网络超时重试 1 次；第三方返回异常/签名失败统一抛异常，由 Controller 转 ApiResponse.Fail；
///      无论成功失败都写 logistics_query_log 落库（失败记录 error_message）。
///   5) 演示降级：未配置真实密钥（Key 为空）或 Mock=true 时，返回模拟轨迹数据（IsMock=true），
///      保证在没有第三方账号的情况下前端功能可开发、可演示。配置好密钥后把 Mock 设为 false 即走真实接口。
/// </summary>
public class LogisticsService : ILogisticsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly ILogger<LogisticsService> _logger;

    // ---- 配置项（来自 appsettings.json 的 Logistics 节）----
    private readonly string _provider;
    private readonly string _key;
    private readonly string _customer;
    private readonly string _secret;
    private readonly int _cacheMinutes;
    private readonly bool _mock;
    private readonly string _trackUrl = "https://poll.kuaidi100.com/poll/query.do";

    /// <summary>常用快递公司编码 → 中文名映射（识别结果 com 缺友好名时兜底）</summary>
    private static readonly Dictionary<string, string> CarrierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shunfeng"] = "顺丰速运", ["yuantong"] = "圆通速递", ["zhongtong"] = "中通快递",
        ["shentong"] = "申通快递", ["yunda"] = "韵达快递", ["jd"] = "京东物流",
        ["jtexpress"] = "极兔速递", ["ems"] = "EMS", ["youzhengguonei"] = "邮政快递包裹",
        ["debon"] = "德邦快递", ["tiantian"] = "天天快递", ["huitongkuaidi"] = "百世快递",
        ["zhaijisong"] = "宅急送", ["youshu"] = "优速快递", ["suning"] = "苏宁物流",
        ["yto"] = "圆通速递", ["zto"] = "中通快递", ["sto"] = "申通快递", ["yd"] = "韵达快递",
        ["auto"] = "智能识别"
    };

    /// <summary>快递100 state → 中文描述</summary>
    private static readonly Dictionary<int, string> StateTexts = new()
    {
        [0] = "在途", [1] = "揽收", [2] = "疑难", [3] = "签收",
        [4] = "退签", [5] = "派件", [6] = "退回"
    };

    public LogisticsService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<LogisticsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _db = db;
        _logger = logger;

        var sec = configuration.GetSection("Logistics");
        _provider = sec["Provider"] ?? "Kuaidi100";
        _key = sec["Key"] ?? string.Empty;
        _customer = sec["Customer"] ?? string.Empty;
        _secret = sec["Secret"] ?? string.Empty;
        _cacheMinutes = int.TryParse(sec["CacheMinutes"], out var c) && c > 0 ? c : 60;

        // 未配置真实密钥时自动进入演示模式，保证开箱即可用
        _mock = string.Equals(sec["Mock"], "true", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(_key);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<LogisticsIdentifyResult>> IdentifyAsync(string trackingNumber)
    {
        if (_mock)
        {
            return ApiResponse<LogisticsIdentifyResult>.Success(new LogisticsIdentifyResult
            {
                TrackingNumber = trackingNumber,
                CarrierCode = "auto",
                CarrierName = "智能识别（演示模式）",
                IsMock = true
            });
        }

        var (code, name) = await IdentifyCoreAsync(trackingNumber);
        if (string.IsNullOrWhiteSpace(code))
        {
            return ApiResponse<LogisticsIdentifyResult>.Fail("无法识别快递公司，请检查单号是否正确", 404);
        }

        return ApiResponse<LogisticsIdentifyResult>.Success(new LogisticsIdentifyResult
        {
            TrackingNumber = trackingNumber,
            CarrierCode = code,
            CarrierName = name,
            IsMock = false
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<LogisticsTrackResult>> TrackAsync(string trackingNumber, string? carrierCode = null, long? orderId = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return ApiResponse<LogisticsTrackResult>.Fail("快递单号不能为空");
        }

        trackingNumber = trackingNumber.Trim();
        var cacheKey = $"logistics:track:{trackingNumber}";

        // 命中缓存：直接返回，不调用第三方（避免重复扣费）
        if (_cache.TryGetValue<LogisticsTrackResult>(cacheKey, out var cached) && cached != null)
        {
            return ApiResponse<LogisticsTrackResult>.Success(cached);
        }

        // 未指定承运商则先智能识别
        var com = string.IsNullOrWhiteSpace(carrierCode) ? string.Empty : carrierCode.Trim();
        if (string.IsNullOrEmpty(com) && !_mock)
        {
            (com, _) = await IdentifyCoreAsync(trackingNumber);
        }

        LogisticsTrackResult result;
        try
        {
            result = _mock
                ? BuildMock(trackingNumber, com)
                : await QueryTrackAsync(trackingNumber, com);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "物流轨迹查询失败，单号={TrackingNumber}", trackingNumber);
            await LogQueryAsync(orderId, trackingNumber, com, string.Empty, false, ex.Message);
            return ApiResponse<LogisticsTrackResult>.Fail($"物流查询失败：{ex.Message}");
        }

        // 写入缓存（TTL = CacheMinutes 分钟）
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_cacheMinutes));

        await LogQueryAsync(orderId, trackingNumber, result.CarrierCode, JsonSerializer.Serialize(result), true, string.Empty);

        return ApiResponse<LogisticsTrackResult>.Success(result);
    }

    /// <inheritdoc/>
    public Task<ApiResponse<LogisticsMapResult>> GetMapAsync(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return Task.FromResult(ApiResponse<LogisticsMapResult>.Fail("快递单号不能为空"));
        }

        trackingNumber = trackingNumber.Trim();

        // 演示模式 / 无独立地图能力时返回空 URL，前端不展示地图按钮
        if (_mock)
        {
            return Task.FromResult(ApiResponse<LogisticsMapResult>.Success(new LogisticsMapResult
            {
                TrackingNumber = trackingNumber,
                MapUrl = string.Empty
            }));
        }

        // 快递100 实时查询无独立地图链接；返回其公开查件页（可在 iframe 内展示轨迹）。
        // 若改用快递鸟「地图轨迹订阅推送」，可在此替换为对应链接。
        var url = $"https://www.kuaidi100.com/auto/?nu={Uri.EscapeDataString(trackingNumber)}";
        return Task.FromResult(ApiResponse<LogisticsMapResult>.Success(new LogisticsMapResult
        {
            TrackingNumber = trackingNumber,
            MapUrl = url
        }));
    }

    // ============================================================
    // 内部：第三方调用与解析
    // ============================================================

    /// <summary>
    /// 智能识别承运商（快递100 com=auto）。
    /// 返回 (code, name)；识别失败 code 为空。
    /// </summary>
    private async Task<(string code, string name)> IdentifyCoreAsync(string trackingNumber)
    {
        var param = JsonSerializer.Serialize(new
        {
            com = "auto",
            num = trackingNumber,
            resultv2 = 1,
            show = "0"
        });

        var form = BuildForm(param);
        using var client = _httpClientFactory.CreateClient("kuaidi");

        var resp = await client.PostAsync(_trackUrl, form);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var code = root.TryGetProperty("com", out var c) ? (c.GetString() ?? string.Empty) : string.Empty;
        return (code, CarrierName(code));
    }

    /// <summary>
    /// 调用快递100 实时查询接口，解析为统一轨迹模型。
    /// 超时（TaskCanceledException）重试 1 次。
    /// </summary>
    private async Task<LogisticsTrackResult> QueryTrackAsync(string trackingNumber, string com)
    {
        var param = JsonSerializer.Serialize(new
        {
            com = string.IsNullOrWhiteSpace(com) ? "auto" : com,
            num = trackingNumber,
            resultv2 = 1,
            show = "0"
        });

        var form = BuildForm(param);
        using var client = _httpClientFactory.CreateClient("kuaidi");

        HttpResponseMessage resp;
        try
        {
            resp = await client.PostAsync(_trackUrl, form);
        }
        catch (TaskCanceledException) // 网络超时
        {
            _logger.LogWarning("物流查询超时，重试 1 次，单号={TrackingNumber}", trackingNumber);
            resp = await client.PostAsync(_trackUrl, form); // 重试一次
        }

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var message = root.TryGetProperty("message", out var m) ? (m.GetString() ?? string.Empty) : string.Empty;
        var status = root.TryGetProperty("status", out var st) ? (st.GetString() ?? string.Empty) : string.Empty;

        // 业务失败（非 200，且无 data 数组）
        if (status != "200" && !root.TryGetProperty("data", out _))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "第三方接口返回异常" : message);
        }

        var comRes = root.TryGetProperty("com", out var c) ? (c.GetString() ?? com) : com;
        var nu = root.TryGetProperty("nu", out var n) ? (n.GetString() ?? trackingNumber) : trackingNumber;
        var state = ParseState(root);

        var nodes = new List<LogisticsTrackNode>();
        if (root.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                var time = (item.TryGetProperty("ftime", out var ft) && ft.ValueKind == JsonValueKind.String ? ft.GetString()
                          : item.TryGetProperty("time", out var t) ? t.GetString() : null) ?? string.Empty;
                var context = item.TryGetProperty("context", out var ctx) ? (ctx.GetString() ?? string.Empty) : string.Empty;
                var location = item.TryGetProperty("location", out var loc) ? (loc.GetString() ?? string.Empty) : string.Empty;
                nodes.Add(new LogisticsTrackNode { Time = time, Context = context, Location = location });
            }
        }

        // 快递100 返回的是正向时间顺序，倒序成「最新在前」
        nodes.Reverse();

        return new LogisticsTrackResult
        {
            TrackingNumber = nu,
            CarrierCode = comRes,
            CarrierName = CarrierName(comRes),
            State = state,
            StateText = StateText(state),
            Nodes = nodes,
            EstimatedDelivery = null, // 快递100 实时查询不返回预估送达时间
            IsMock = false,
            QueryTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    /// <summary>
    /// 构造快递100 表单（customer + param + sign）。
    /// 签名算法（快递100 实时查询 poll/query.do）：
    ///   sign = MD5( param + customer + key ) 转大写。
    /// ⚠️ 若平台返回「签名错误/校验失败」，把拼接顺序换成 param + key + customer 再试
    ///   （新旧两版接口签名顺序不同，以你所注册平台「开发文档」为准）。
    /// </summary>
    private FormUrlEncodedContent BuildForm(string param)
    {
        var sign = Md5($"{param}{_customer}{_key}").ToUpper();
        return new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("customer", _customer),
            new KeyValuePair<string, string>("param", param),
            new KeyValuePair<string, string>("sign", sign)
        });
    }

    // ============================================================
    // 内部：演示数据 / 工具
    // ============================================================

    /// <summary>未配置密钥时构造一条逼真的演示轨迹，保证前端可展示</summary>
    private LogisticsTrackResult BuildMock(string trackingNumber, string com)
    {
        var carrierName = string.IsNullOrWhiteSpace(com) ? "演示快递" : CarrierName(com);
        var now = DateTime.Now;
        var nodes = new List<LogisticsTrackNode>
        {
            new() { Time = now.ToString("yyyy-MM-dd HH:mm:ss"), Context = "【广州市】您的订单已签收，签收人：本人。感谢使用，期待再次为您服务" },
            new() { Time = now.AddHours(-6).ToString("yyyy-MM-dd HH:mm:ss"), Context = "【深圳市】快件已到达 深圳转运中心" },
            new() { Time = now.AddHours(-20).ToString("yyyy-MM-dd HH:mm:ss"), Context = "【上海市】快件已发车，下一站 深圳转运中心" },
            new() { Time = now.AddHours(-28).ToString("yyyy-MM-dd HH:mm:ss"), Context = "【上海市】顺丰已收取快件（演示数据）" }
        };

        return new LogisticsTrackResult
        {
            TrackingNumber = trackingNumber,
            CarrierCode = string.IsNullOrWhiteSpace(com) ? "demo" : com,
            CarrierName = carrierName,
            State = 3,
            StateText = "签收",
            Nodes = nodes,
            EstimatedDelivery = now.ToString("yyyy-MM-dd"),
            IsMock = true,
            QueryTime = now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    /// <summary>落库查询日志（成功/失败都记；失败不影响主流程）</summary>
    private async Task LogQueryAsync(long? orderId, string trackingNumber, string carrierCode, string result, bool success, string error)
    {
        try
        {
            _db.LogisticsQueryLogs.Add(new LogisticsQueryLog
            {
                OrderId = orderId ?? 0,
                TrackingNumber = trackingNumber,
                CarrierCode = carrierCode,
                CarrierName = CarrierName(carrierCode),
                QueryResult = result,
                Status = success ? 1 : 0,
                ErrorMessage = error,
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入物流查询日志失败（不影响主流程），单号={TrackingNumber}", trackingNumber);
        }
    }

    private static string CarrierName(string code)
        => string.IsNullOrWhiteSpace(code) ? "未知快递" : (CarrierNames.TryGetValue(code, out var n) ? n : code);

    private static string StateText(int state)
        => StateTexts.TryGetValue(state, out var t) ? t : "未知";

    /// <summary>兼容快递100 state 既可能是数字也可能是字符串</summary>
    private static int ParseState(JsonElement root)
    {
        if (!root.TryGetProperty("state", out var s)) return 0;
        return s.ValueKind switch
        {
            JsonValueKind.Number => s.GetInt32(),
            JsonValueKind.String => int.TryParse(s.GetString(), out var v) ? v : 0,
            _ => 0
        };
    }

    /// <summary>MD5 → 32 位小写十六进制</summary>
    private static string Md5(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
