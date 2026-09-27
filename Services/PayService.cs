using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jsd.Api.Services;

/// <summary>
/// 微信支付 主动查单与补单 服务实现。
///
/// 设计要点（对照微信支付 V3 官方文档 + 本项目资金流约定）：
///   1) 回调验签：用【微信平台证书公钥】验 RSA-SHA256 签名，防伪造回调（替换了原 Finance 模块的占位 VerifySign）。
///   2) 回调解密：resource.ciphertext 用 APIv3 密钥做 AES-256-GCM 解密。
///   3) 幂等：out_trade_no 唯一索引 + 终态幂等（已 SUCCESS 直接返回，不重复写库/不重复推进业务）。
///      高并发用 ExecuteUpdateAsync 条件更新（status 非 SUCCESS 才改），规避"两个回调同时查到存在然后都插入"的竞态。
///   4) 主动查单（补单）：定时任务 / 后台手动 共用 QueryOrderFromWeChatAsync，微信 SUCCESS → 本地 SUCCESS；
///      微信未支付且超时 → 本地 CLOSED 关单（关单前仍先问一次微信，避免误关已支付单）。
///   5) 异常：任何网络/解析异常都被捕获并写入 pay_callback_log(handle_status=2)，再转 ApiResponse.Fail，
///      绝不抛未捕获异常导致进程崩溃；定时任务侧再逐单 try-catch，单笔失败不影响整体扫描。
/// </summary>
public class PayService : IPayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayService> _logger;

    // 微信支付 V3 配置（来自 appsettings.json 的 WeChatPay 节）
    private readonly string _mchId;
    private readonly string _appId;
    private readonly string _apiV3Key;
    private readonly string _merchantPrivateKey;
    private readonly string _merchantSerialNo;
    private readonly string _platformPublicKey;
    private readonly string _baseUrl;
    private readonly string _notifyUrl;

    public PayService(
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<PayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _configuration = configuration;
        _logger = logger;

        var sec = configuration.GetSection("WeChatPay");
        _mchId = sec["MchId"] ?? string.Empty;
        _appId = sec["AppId"] ?? string.Empty;
        _apiV3Key = sec["ApiV3Key"] ?? string.Empty;
        _merchantPrivateKey = sec["MerchantPrivateKey"] ?? string.Empty;
        _merchantSerialNo = sec["MerchantSerialNo"] ?? string.Empty;
        _platformPublicKey = sec["PlatformPublicKey"] ?? string.Empty;
        _baseUrl = string.IsNullOrWhiteSpace(sec["BaseUrl"]) ? "https://api.mch.weixin.qq.com" : sec["BaseUrl"]!;
        _notifyUrl = sec["NotifyUrl"] ?? string.Empty;
    }

    /// <summary>是否已正确配置（未配置时回调/查单直接 Fail，避免无意义异常）</summary>
    private bool IsConfigured => !string.IsNullOrWhiteSpace(_mchId)
                                 && !string.IsNullOrWhiteSpace(_apiV3Key)
                                 && !string.IsNullOrWhiteSpace(_merchantPrivateKey)
                                 && !string.IsNullOrWhiteSpace(_platformPublicKey);

    // ============================================================
    // 1. 微信支付结果回调（V3）
    // ============================================================

    public async Task<ApiResponse<PayNotifyResultDto>> HandleNotifyCallbackAsync(HttpRequest request)
    {
        // ---- 读取原始请求体（微信回调是 JSON，必须读原文用于验签）----
        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        // ---- 读取微信回调签名头 ----
        var timestamp = request.Headers["Wechatpay-Timestamp"].ToString();
        var nonce = request.Headers["Wechatpay-Nonce"].ToString();
        var signature = request.Headers["Wechatpay-Signature"].ToString();
        var serial = request.Headers["Wechatpay-Serial"].ToString();

        // 未配置 / 缺签名头 → 直接判失败（微信会按非 200 重试）
        if (!IsConfigured)
        {
            await WriteCallbackLogAsync(body, null, (int)CallbackHandleStatus.Failed, "微信支付未配置（WeChatPay 节缺失）");
            return ApiResponse<PayNotifyResultDto>.Fail("微信支付未配置", 500);
        }

        // ---- 验签（用微信平台证书公钥）----
        if (!WeChatPayCrypto.VerifySignature(timestamp, nonce, body, signature, _platformPublicKey))
        {
            _logger.LogWarning("微信支付回调验签失败，serial={Serial}", serial);
            await WriteCallbackLogAsync(body, null, (int)CallbackHandleStatus.Failed, "验签失败");
            return ApiResponse<PayNotifyResultDto>.Fail("验签失败", 401);
        }

        // ---- 解密 resource → 交易对象 ----
        WeChatPayTransactionDto? txn;
        try
        {
            txn = DecodeNotifyBody(body, _apiV3Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "微信支付回调解密失败");
            await WriteCallbackLogAsync(body, null, (int)CallbackHandleStatus.Failed, $"解密失败：{ex.Message}");
            return ApiResponse<PayNotifyResultDto>.Fail("回调解密失败", 500);
        }

        // ---- 幂等 + 状态推进 ----
        try
        {
            var r = await ApplyWeChatResultAsync(txn, isCallback: true);

            // 写回调日志（成功 / 幂等忽略）
            await WriteCallbackLogAsync(body, txn.TransactionId,
                r.Duplicated ? (int)CallbackHandleStatus.Ignored : (int)CallbackHandleStatus.Success,
                r.Msg);

            return ApiResponse<PayNotifyResultDto>.Success(new PayNotifyResultDto
            {
                PaymentOrderId = r.Id,
                OutTradeNo = txn.OutTradeNo,
                TransactionId = txn.TransactionId,
                Status = r.Status,
                Duplicated = r.Duplicated,
                Message = r.Msg
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "微信支付回调业务处理异常，outTradeNo={OutTradeNo}", txn.OutTradeNo);
            await WriteCallbackLogAsync(body, txn.TransactionId, (int)CallbackHandleStatus.Failed, $"处理异常：{ex.Message}");
            return ApiResponse<PayNotifyResultDto>.Fail($"处理失败：{ex.Message}", 500);
        }
    }

    // ============================================================
    // 2. 主动查单 / 补单（V3 查询订单 API）
    // ============================================================

    public async Task<ApiResponse<PayQueryResultDto>> QueryOrderFromWeChatAsync(string outTradeNo)
    {
        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            return ApiResponse<PayQueryResultDto>.Fail("商户订单号不能为空");
        }

        if (!IsConfigured)
        {
            return ApiResponse<PayQueryResultDto>.Fail("微信支付未配置（WeChatPay 节缺失）", 500);
        }

        outTradeNo = outTradeNo.Trim();

        // ---- 调微信查询订单 API ----
        WeChatPayTransactionDto? txn;
        try
        {
            txn = await QueryWeChatAsync(outTradeNo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "微信查单失败，outTradeNo={OutTradeNo}", outTradeNo);
            return ApiResponse<PayQueryResultDto>.Fail($"微信查单失败：{ex.Message}");
        }

        // 404 = 订单不存在 / 未支付：构造一个 NOTPAY 的交易对象，交给统一状态机判断是否关单
        txn ??= new WeChatPayTransactionDto { OutTradeNo = outTradeNo, TradeState = "NOTPAY" };

        // ---- 同步本地状态（成功补单 / 超时关单 / 未变动）----
        try
        {
            var r = await ApplyWeChatResultAsync(txn, isCallback: false);
            return ApiResponse<PayQueryResultDto>.Success(new PayQueryResultDto
            {
                OutTradeNo = outTradeNo,
                TransactionId = r.TransactionId,
                TradeState = txn.TradeState,
                Status = r.Status,
                Action = r.Action,
                Message = r.Msg
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "微信查单结果同步失败，outTradeNo={OutTradeNo}", outTradeNo);
            return ApiResponse<PayQueryResultDto>.Fail($"同步失败：{ex.Message}");
        }
    }

    // ============================================================
    // 3. 创建支付单（业务发起支付时调用）
    // ============================================================

    public async Task<ApiResponse<long>> CreatePaymentOrderAsync(long bizOrderId, string outTradeNo, long amount, int payChannel, DateTime expireTime)
    {
        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            return ApiResponse<long>.Fail("商户订单号不能为空");
        }

        // out_trade_no 唯一索引兜底：若并发插入重复，EF 抛 DbUpdateException，这里转业务失败
        var order = new PaymentOrder
        {
            BizOrderId = bizOrderId,
            OutTradeNo = outTradeNo.Trim(),
            Amount = amount,
            PayChannel = payChannel,
            Status = (int)PayOrderStatus.Paying,   // 创建即进入支付中
            ExpireTime = expireTime,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };

        try
        {
            _db.PaymentOrders.Add(order);
            await _db.SaveChangesAsync();
            return ApiResponse<long>.Success(order.Id, "支付单创建成功");
        }
        catch (DbUpdateException ex)
        {
            // 唯一索引冲突 → 视为重复创建，返回失败由调用方决定是否幂等复用
            _logger.LogWarning(ex, "支付单创建失败（可能 out_trade_no 重复），outTradeNo={OutTradeNo}", outTradeNo);
            return ApiResponse<long>.Fail($"支付单创建失败：{ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // ============================================================
    // 内部：解析 & 调用微信
    // ============================================================

    /// <summary>解密微信回调 JSON，得到交易对象（trade_state 等）</summary>
    private static WeChatPayTransactionDto DecodeNotifyBody(string body, string apiV3Key)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // resource 才是密文；event_type 等在外层，仅用于日志参考
        var resource = root.GetProperty("resource");
        var algorithm = resource.GetProperty("algorithm").GetString();
        if (!string.Equals(algorithm, "AEAD_AES_256_GCM", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"不支持的加密算法：{algorithm}");
        }

        var ciphertext = resource.GetProperty("ciphertext").GetString()!;
        var nonce = resource.GetProperty("nonce").GetString()!;
        var associatedData = resource.TryGetProperty("associated_data", out var ad) ? ad.GetString() : null;

        var plain = WeChatPayCrypto.DecryptResource(ciphertext, nonce, associatedData, apiV3Key);
        return ParseTransaction(plain);
    }

    /// <summary>调用微信「查询订单 API」（按商户订单号查询）：GET /v3/pay/transactions/out-trade-no/{out_trade_no}</summary>
    private async Task<WeChatPayTransactionDto?> QueryWeChatAsync(string outTradeNo)
    {
        var encodedNo = Uri.EscapeDataString(outTradeNo);
        var path = $"/v3/pay/transactions/out-trade-no/{encodedNo}";
        var url = $"{_baseUrl.TrimEnd('/')}{path}";

        var auth = WeChatPayCrypto.BuildAuthorization("GET", path, string.Empty, new WeChatPayOptions
        {
            MchId = _mchId,
            MerchantPrivateKey = _merchantPrivateKey,
            MerchantSerialNo = _merchantSerialNo
        });

        using var client = _httpClientFactory.CreateClient("wechatpay");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Authorization", auth);
        req.Headers.Add("Accept", "application/json");

        using var resp = await client.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();

        // 404 = 订单不存在 / 未支付（微信对不存在的 out_trade_no 返回 404）
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"微信查单返回 {(int)resp.StatusCode}：{respBody}");
        }

        return ParseTransaction(respBody);
    }

    /// <summary>解析微信交易对象 JSON（回调解密后的明文 / 查单响应 同为交易对象结构）</summary>
    private static WeChatPayTransactionDto ParseTransaction(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var txn = new WeChatPayTransactionDto
        {
            OutTradeNo = root.TryGetProperty("out_trade_no", out var o) ? o.GetString() ?? string.Empty : string.Empty,
            TransactionId = root.TryGetProperty("transaction_id", out var t) ? t.GetString() : null,
            TradeState = root.TryGetProperty("trade_state", out var s) ? s.GetString() ?? string.Empty : string.Empty,
            TradeStateDesc = root.TryGetProperty("trade_state_desc", out var d) ? d.GetString() : null,
            SuccessTime = root.TryGetProperty("success_time", out var st) ? st.GetString() : null
        };

        if (root.TryGetProperty("amount", out var amt) && amt.TryGetProperty("total", out var total))
        {
            txn.Total = total.GetInt32();
        }

        return txn;
    }

    // ============================================================
    // 内部：幂等 + 状态推进（回调与查单共用）
    // ============================================================

    /// <summary>
    /// 根据微信侧结果推进本地支付单状态，并保证幂等：
    ///   - 微信 SUCCESS → 本地更新为 SUCCESS（条件更新：仅当当前非 SUCCESS 才改，ExecuteUpdate 返回 0 即已处理过）。
    ///   - 微信未支付（null/NOTPAY/...）：
    ///       · 若已超时（now &gt; expire_time）→ 本地更新为 CLOSED（关单）；
    ///       · 否则保持 PAYING 不变。
    /// 返回推进后的支付单关键信息 + 是否幂等命中 + 动作 + 描述。
    /// ⚠️ 所有对 status 的写都用 ExecuteUpdateAsync 条件更新 + 投影查询回读，避免 EF 变更跟踪器读到过期值。
    /// </summary>
    private async Task<PayApplyResult> ApplyWeChatResultAsync(WeChatPayTransactionDto? txn, bool isCallback)
    {
        var outTradeNo = txn?.OutTradeNo ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            throw new InvalidOperationException("交易对象缺少 out_trade_no，无法定位支付单");
        }

        var paid = string.Equals(txn!.TradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

        if (paid)
        {
            // 条件更新：只有当前状态不是 SUCCESS 才改成 SUCCESS（高并发安全，已 SUCCESS 返回 0）
            var updated = await _db.PaymentOrders
                .Where(p => p.OutTradeNo == outTradeNo && p.Status != (int)PayOrderStatus.Success)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.Status, (int)PayOrderStatus.Success)
                    .SetProperty(x => x.TransactionId, txn.TransactionId)
                    .SetProperty(x => x.UpdateTime, DateTime.Now));

            // 投影查询回读（不依赖变更跟踪器的可能过期值）
            var row = await _db.PaymentOrders.Where(p => p.OutTradeNo == outTradeNo)
                .Select(p => new { p.Id, p.TransactionId, p.Status }).FirstOrDefaultAsync();

            var status = row?.Status ?? (int)PayOrderStatus.Success;
            var duplicated = updated == 0 && status == (int)PayOrderStatus.Success;
            return new PayApplyResult(
                row?.Id ?? 0,
                row?.TransactionId,
                status,
                duplicated,
                "SUCCESS",
                isCallback ? "支付成功，已更新支付单" : "补单成功，已更新为支付成功");
        }

        // ---- 未支付：判断是否超时关单 ----
        var existing = await _db.PaymentOrders.Where(p => p.OutTradeNo == outTradeNo)
            .Select(p => new { p.Id, p.TransactionId, p.Status, p.ExpireTime }).FirstOrDefaultAsync();
        if (existing == null)
        {
            throw new InvalidOperationException($"支付单不存在：{outTradeNo}");
        }

        if (existing.Status == (int)PayOrderStatus.Success)
        {
            // 本地已是成功态，微信这次回的是旧的非 SUCCESS 状态，以本地终态为准
            return new PayApplyResult(existing.Id, existing.TransactionId, existing.Status, true, "UNCHANGED",
                "本地已是支付成功，忽略微信非成功状态");
        }

        if (DateTime.Now > existing.ExpireTime)
        {
            // 已超时且微信侧未支付 → 关单（条件更新，避免覆盖 SUCCESS）
            var closed = await _db.PaymentOrders
                .Where(p => p.OutTradeNo == outTradeNo && p.Status != (int)PayOrderStatus.Success)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.Status, (int)PayOrderStatus.Closed)
                    .SetProperty(x => x.UpdateTime, DateTime.Now));

            var finalStatus = closed > 0 ? (int)PayOrderStatus.Closed : existing.Status;
            return new PayApplyResult(existing.Id, existing.TransactionId, finalStatus, closed == 0, "CLOSED",
                closed > 0 ? "已超时且微信未支付，本地关单" : "已超时关单（此前已处理）");
        }

        // 未超时且未支付：保持 PAYING，等下次查单 / 回调
        return new PayApplyResult(existing.Id, existing.TransactionId, existing.Status, false, "UNCHANGED",
            "订单仍在支付中（未超时），本次未变动");
    }

    /// <summary>状态推进结果（内部聚合返回，避免变更跟踪器过期值问题）</summary>
    private sealed record PayApplyResult(long Id, string? TransactionId, int Status, bool Duplicated, string Action, string Msg);

    // ============================================================
    // 内部：写回调日志（失败也不阻断主流程）
    // ============================================================

    private async Task WriteCallbackLogAsync(string requestBody, string? transactionId, int handleStatus, string responseResult)
    {
        try
        {
            _db.PayCallbackLogs.Add(new PayCallbackLog
            {
                RequestBody = requestBody,
                TransactionId = transactionId,
                HandleStatus = handleStatus,
                ResponseResult = responseResult,
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入 pay_callback_log 失败（不影响主流程）");
        }
    }
}
