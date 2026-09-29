using System.Globalization;
using System.IO.Compression;
using System.Net;
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
/// 微信支付 T+1 对账 服务实现（ReconciliationService）
///
/// ┌── 为什么必须做 T+1 对账 ────────────────────────────────────────────┐
/// │ 支付链路天生存在「不可靠」环节：网络丢包导致回调没到、回调到了但业务       │
/// │ 处理失败、用户支付后系统没收到任何通知（掉单）、商户金额与微信不一致…     │
/// │ 单纯依赖回调 + 主动查单只能覆盖「单笔」，无法确认「一天里到底差了多少」。 │
/// │ 财务对账的做法是：以【微信账单】为权威外部账，与【系统本地支付成功流水】   │
/// │ 逐笔勾稽，把勾不上的（长款/短款/金额不符/状态不符）落到待核查表人工跟进。 │
/// └─────────────────────────────────────────────────────────────────────┘
///
/// 本服务实现要点：
///   1) DownloadDailyBillAsync：
///      V3「申请交易账单」→ 拿 download_url → 下载（gzip，也可能明文）→ 解压 →
///      按【列名】解析 CSV（不写死列序号，微信加列也不炸）→ 明细批量落 wechat_bill_detail。
///      幂等：同一 (bill_date, bill_type) 走 UPDATE 而非 INSERT；重拉会先清旧明细。
///   2) AutoReconciliationAsync：
///      系统侧 SUCCESS 流水（payment_order）与微信明细（wechat_bill_detail）按 out_trade_no
///      双向勾稽 → 差异写 reconciliation_check（重跑先清旧差异，保证幂等）→ 更新主表并出报告。
///   3) 异常纪律：网络/解析/写库异常全部捕获 → 账务回写 wechat_daily_bill.recon_status=3(异常)
///      并记 error_msg，再转 ApiResponse.Fail，绝不把异常抛到宿主进程。
/// </summary>
public class ReconciliationService : IReconciliationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReconciliationService> _logger;

    // 微信支付 V3 配置（appsettings.json 的 WeChatPay 节，与 PayService 同源）
    private readonly string _mchId;
    private readonly string _merchantPrivateKey;
    private readonly string _merchantSerialNo;
    private readonly string _baseUrl;

    /// <summary>明细批量入库的分批大小（避免单次 SaveChanges 塞上万条导致 MySQL packet 过大）</summary>
    private const int BatchSize = 500;

    public ReconciliationService(
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<ReconciliationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _configuration = configuration;
        _logger = logger;

        var sec = configuration.GetSection("WeChatPay");
        _mchId = sec["MchId"] ?? string.Empty;
        _merchantPrivateKey = sec["MerchantPrivateKey"] ?? string.Empty;
        _merchantSerialNo = sec["MerchantSerialNo"] ?? string.Empty;
        _baseUrl = string.IsNullOrWhiteSpace(sec["BaseUrl"]) ? "https://api.mch.weixin.qq.com" : sec["BaseUrl"]!;
    }

    /// <summary>是否已正确配置（未配置时直接 Fail，不打扰其它模块）</summary>
    private bool IsConfigured => !string.IsNullOrWhiteSpace(_mchId)
                                 && !string.IsNullOrWhiteSpace(_merchantPrivateKey)
                                 && !string.IsNullOrWhiteSpace(_merchantSerialNo);

    // ============================================================
    // 一、下载账单：申请 → 下载 → 解压 → 解析 → 入库
    // ============================================================

    public async Task<ApiResponse<BillDownloadDto>> DownloadDailyBillAsync(DateTime billDate, int billType = (int)BillType.All, bool force = false)
    {
        var date = billDate.Date;
        var typeText = BillTypeNames.ToWeChatString(billType);

        if (date >= DateTime.Today)
        {
            // 微信规则：当日账单次日才生成。凌晨跑的任务拿的一定是「昨天」，这里只提示不阻断，
            // 微信会对这类请求返回错误，由上层把具体原因呈现出来。
            _logger.LogWarning("账单日期 {Date} 不是历史日期，微信通常要到次日才生成该日账单",
                date.ToString("yyyy-MM-dd"));
        }

        if (!IsConfigured)
        {
            return ApiResponse<BillDownloadDto>.Fail("微信支付未配置（WeChatPay 节缺失 MchId/私钥/证书序列号）", 500);
        }

        // ---- 1. 幂等：本地已有且非强制 → 直接复用 ----
        var bill = await _db.WechatDailyBills.FirstOrDefaultAsync(b => b.BillDate == date && b.BillType == typeText);
        if (bill != null && !force && bill.TotalCount > 0)
        {
            return ApiResponse<BillDownloadDto>.Success(new BillDownloadDto
            {
                BillId = bill.Id,
                BillDate = date.ToString("yyyy-MM-dd"),
                BillType = typeText,
                TotalCount = bill.TotalCount,
                TotalAmount = bill.TotalAmount,
                RefundAmount = bill.RefundAmount,
                TotalFee = bill.TotalFee,
                ReconStatus = StatusOf(bill!),
                Skipped = true,
                Message = "该日账单本地已存在，跳过下载（force=true 可强制重拉）"
            });
        }

        // ---- 2. 主表占位 / 复用（同一天同类型只有一行，靠 uk_bill_date_type 保证）----
        var isNew = bill == null;
        if (isNew)
        {
            bill = new WechatDailyBill
            {
                BillDate = date,
                BillType = typeText,
                ReconStatus = (int)BillReconStatus.Processing,
                CreateTime = DateTime.Now
            };
            _db.WechatDailyBills.Add(bill);
        }
        else
        {
            SetStatus(bill!, (int)BillReconStatus.Processing);
            bill!.ErrorMsg = null;
        }
        await _db.SaveChangesAsync();

        try
        {
            // ---- 3. 调微信「申请交易账单」接口，拿下载地址 ----
            var (downloadUrl, hashType, hashValue) = await ApplyTradeBillAsync(date, typeText);
            bill.DownloadUrl = downloadUrl;
            bill.HashType = hashType;
            bill.HashValue = hashValue;
            await _db.SaveChangesAsync();

            // ---- 4. 下载账单文件（可能是 gzip，也可能是明文 CSV）----
            var fileBytes = await DownloadBillFileAsync(downloadUrl);
            _logger.LogInformation("微信账单下载完成，日期={Date}，字节数={Size}", date.ToString("yyyy-MM-dd"), fileBytes.Length);

            // ---- 5. 解压 + 解析 CSV ----
            var csv = Decompress(fileBytes);
            var rows = ParseBillCsv(csv);

            // ---- 6. 清旧明细 + 批量写入（重拉必须清，否则明细翻倍）----
            await _db.WechatBillDetails.Where(d => d.BillId == bill.Id).ExecuteDeleteAsync();

            var now = DateTime.Now;
            for (var i = 0; i < rows.Count; i += BatchSize)
            {
                var batch = rows.Skip(i).Take(BatchSize).Select(r => new WechatBillDetail
                {
                    BillId = bill.Id,
                    TransactionId = string.IsNullOrWhiteSpace(r.TransactionId) ? null : r.TransactionId,
                    OutTradeNo = string.IsNullOrWhiteSpace(r.OutTradeNo) ? null : r.OutTradeNo,
                    TradeTime = r.TradeTime,
                    TradeState = string.IsNullOrWhiteSpace(r.TradeState) ? null : r.TradeState,
                    TotalFee = r.TotalFee,
                    RefundFee = r.RefundFee,
                    Poundage = r.Poundage,
                    CreateTime = now
                });
                _db.WechatBillDetails.AddRange(batch);
                await _db.SaveChangesAsync();
            }

            // ---- 7. 回填主表统计（从明细行自己算，比依赖账单汇总行更可靠）----
            bill.TotalCount = rows.Count;
            bill.TotalAmount = rows.Sum(r => r.TotalFee);
            bill.RefundAmount = rows.Sum(r => r.RefundFee);
            bill.TotalFee = rows.Sum(r => r.Poundage);
            SetStatus(bill!, rows.Count == 0
                ? (int)BillReconStatus.Finished      // 当天确实没有交易：空账单也算处理完成
                : (int)BillReconStatus.Processing);  // 有明细但未比对，等 AutoReconciliation 完成
            bill.ReconResult = rows.Count == 0 ? "当日无交易记录" : null;
            bill.ErrorMsg = null;
            await _db.SaveChangesAsync();

            return ApiResponse<BillDownloadDto>.Success(new BillDownloadDto
            {
                BillId = bill.Id,
                BillDate = date.ToString("yyyy-MM-dd"),
                BillType = typeText,
                TotalCount = bill.TotalCount,
                TotalAmount = bill.TotalAmount,
                RefundAmount = bill.RefundAmount,
                TotalFee = bill.TotalFee,
                ReconStatus = StatusOf(bill!),
                Skipped = false,
                Message = $"账单下载并入库成功：{rows.Count} 笔（请继续执行自动对账）"
            });
        }
        catch (Exception ex)
        {
            // 下载/解析/入库任一环节失败：主表标记异常，便于次日任务或人工重试
            _logger.LogError(ex, "微信账单下载/解析失败，日期={Date}", date.ToString("yyyy-MM-dd"));
            await MarkBillErrorAsync(bill, ex.Message);
            return ApiResponse<BillDownloadDto>.Fail($"账单下载失败：{ex.Message}");
        }
    }

    // ============================================================
    // 二、自动对账：双向勾稽 + 差异落表 + 出报告
    // ============================================================

    public async Task<ApiResponse<ReconciliationReportDto>> AutoReconciliationAsync(DateTime billDate, bool forceDownload = false)
    {
        var date = billDate.Date;

        try
        {
            // ---- 1. 确保账单明细已经落到本地（没有 / 之前失败 / 强制 → 先下载）----
            var bill = await _db.WechatDailyBills
                .FirstOrDefaultAsync(b => b.BillDate == date && b.BillType == "ALL");

            var needDownload = bill == null || forceDownload || StatusOf(bill!) == (int)BillReconStatus.Error;
            if (needDownload)
            {
                var dl = await DownloadDailyBillAsync(date, (int)BillType.All, forceDownload);
                if (dl.Code != 200 || dl.Data == null)
                {
                    return ApiResponse<ReconciliationReportDto>.Fail($"自动对账失败（前置下载未成功）：{dl.Message}");
                }

                bill = await _db.WechatDailyBills.FirstAsync(b => b.Id == dl.Data.BillId);
            }
            else if (StatusOf(bill!) == (int)BillReconStatus.Pending && bill!.TotalCount == 0)
            {
                // 主表存在但没内容（例如上次下载中断）→ 也补拉一次
                var dl = await DownloadDailyBillAsync(date, (int)BillType.All, true);
                if (dl.Code != 200 || dl.Data == null)
                {
                    return ApiResponse<ReconciliationReportDto>.Fail($"自动对账失败（前置下载未成功）：{dl.Message}");
                }
                bill = await _db.WechatDailyBills.FirstAsync(b => b.Id == dl.Data.BillId);
            }

            SetStatus(bill!, (int)BillReconStatus.Processing);
            bill!.ErrorMsg = null;
            await _db.SaveChangesAsync();

            // ---- 2. 取两侧数据 ----
            // 系统侧：当天支付成功的支付单（payment_order.status = SUCCESS）
            // ⚠️ 口径说明：payment_order 目前只有 create_time（支付单创建时间），没有「支付成功时间」。
            //    对绝大多数场景（创建后几分钟内支付）足够精确；若未来要求严格，建议给 payment_order
            //    增加 pay_time 字段并在 PayService 补单成功时回填，届时把下面的时间条件换成 pay_time 即可。
            var dayStart = date;
            var dayEnd = date.AddDays(1);
            var sysRows = await _db.PaymentOrders
                .Where(p => p.Status == (int)PayOrderStatus.Success && p.CreateTime >= dayStart && p.CreateTime < dayEnd)
                .Select(p => new { p.Id, p.OutTradeNo, p.Amount, p.Status })
                .ToListAsync();

            var wxRows = await _db.WechatBillDetails
                .Where(d => d.BillId == bill.Id)
                .ToListAsync();

            // ---- 3. 建索引：系统侧按 out_trade_no（唯一）；微信侧同单可能多行（支付行 + 退款行）----
            var sysMap = new Dictionary<string, (long Id, long AmountFen, int Status)>(StringComparer.Ordinal);
            foreach (var s in sysRows)
            {
                if (string.IsNullOrWhiteSpace(s.OutTradeNo)) continue;
                sysMap[s.OutTradeNo] = (s.Id, s.Amount, s.Status);
            }

            var wxMap = AggregateWechatRows(wxRows);

            // ---- 4. 双向勾稽 ----
            var diffs = new List<ReconciliationCheck>();
            var handled = new HashSet<string>(StringComparer.Ordinal);

            // 4.1 以微信为准：逐笔找系统
            foreach (var kv in wxMap)
            {
                handled.Add(kv.Key);
                var agg = kv.Value;

                if (!sysMap.TryGetValue(kv.Key, out var sys))
                {
                    // 长款：用户在微信付了钱，系统却没成功流水 → 疑似掉单，优先级最高
                    diffs.Add(BuildDiff(bill.Id, 0, kv.Key, agg.Row, (int)DiffType.WechatOnly, agg.Row.TotalFee, 0m));
                    continue;
                }

                var sysAmountYuan = sys.AmountFen / 100m;   // 分 → 元
                var wechatAmount = agg.Row.TotalFee;

                if (Math.Abs(wechatAmount - sysAmountYuan) > ReconAmountTolerance.Yuan)
                {
                    // 两边都有但金额对不上（可能部分退款、优惠券、币种异常）
                    diffs.Add(BuildDiff(bill.Id, sys.Id, kv.Key, agg.Row, (int)DiffType.AmountDiff, wechatAmount, sysAmountYuan));
                }
                else if (agg.HasRefund || !IsFinalSuccess(agg.Row.TradeState))
                {
                    // 金额一致但状态不符：微信侧已退款/撤销/关闭，系统仍是 SUCCESS
                    diffs.Add(BuildDiff(bill.Id, sys.Id, kv.Key, agg.Row, (int)DiffType.StatusDiff, wechatAmount, sysAmountYuan));
                }
            }

            // 4.2 以系统为准：反查微信没有的 → 短款（系统的钱可能是"假成功"）
            foreach (var kv in sysMap)
            {
                if (handled.Contains(kv.Key)) continue;
                if (wxMap.ContainsKey(kv.Key)) continue;

                diffs.Add(new ReconciliationCheck
                {
                    BillId = bill.Id,
                    OrderId = kv.Value.Id,
                    OutTradeNo = kv.Key,
                    TransactionId = null,
                    DiffType = (int)DiffType.SystemOnly,
                    WechatAmount = 0m,
                    SystemAmount = kv.Value.AmountFen / 100m,
                    WechatStatus = null,
                    SystemStatus = kv.Value.Status,
                    HandleStatus = (int)CheckHandleStatus.Pending,
                    CreateTime = DateTime.Now
                });
            }

            // ---- 5. 差异落库（先清旧差异 → 保证重跑幂等，不会越跑越多）----
            await _db.ReconciliationChecks.Where(c => c.BillId == bill.Id).ExecuteDeleteAsync();
            for (var i = 0; i < diffs.Count; i += BatchSize)
            {
                _db.ReconciliationChecks.AddRange(diffs.Skip(i).Take(BatchSize));
                await _db.SaveChangesAsync();
            }

            // ---- 6. 生成对账报告 ----
            var report = await BuildReportAsync(bill, wxRows, sysRows.Count, sysRows.Sum(s => s.Amount) / 100m, diffs);

            // ---- 7. 主表收口 ----
            SetStatus(bill!, (int)BillReconStatus.Finished);
            bill.ReconResult = report.Summary;
            bill.ErrorMsg = null;
            await _db.SaveChangesAsync();

            _logger.LogInformation("微信T+1对账完成：{Summary}", report.Summary);
            return ApiResponse<ReconciliationReportDto>.Success(report, "对账完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "T+1 自动对账异常，日期={Date}", billDate.ToString("yyyy-MM-dd"));
            await MarkBillErrorByDateAsync(date, ex.Message);
            return ApiResponse<ReconciliationReportDto>.Fail($"自动对账失败：{ex.Message}");
        }
    }

    // ============================================================
    // 三、查询类：报告 / 差异列表 / 人工处理
    // ============================================================

    public async Task<ApiResponse<ReconciliationReportDto>> GetReportAsync(DateTime billDate)
    {
        var date = billDate.Date;

        var bill = await _db.WechatDailyBills
            .FirstOrDefaultAsync(b => b.BillDate == date && b.BillType == "ALL");

        if (bill == null)
        {
            return ApiResponse<ReconciliationReportDto>.Fail($"尚未账单数据：{date:yyyy-MM-dd}（请先执行下载/对账）", 404);
        }

        var wxRows = await _db.WechatBillDetails.Where(d => d.BillId == bill.Id).ToListAsync();

        var sysAmountFen = await _db.PaymentOrders
            .Where(p => p.Status == (int)PayOrderStatus.Success
                        && p.CreateTime >= date && p.CreateTime < date.AddDays(1))
            .SumAsync(p => (long?)p.Amount) ?? 0L;
        var sysCount = await _db.PaymentOrders
            .CountAsync(p => p.Status == (int)PayOrderStatus.Success
                             && p.CreateTime >= date && p.CreateTime < date.AddDays(1));

        var diffs = await LoadDiffsAsync(bill.Id);
        var report = await BuildReportAsync(bill, wxRows, sysCount, sysAmountFen / 100m, diffs);

        return ApiResponse<ReconciliationReportDto>.Success(report);
    }

    public async Task<ApiResponse<PagedResult<ReconCheckDto>>> GetDiffListAsync(DateTime? billDate, int? diffType, int? handleStatus, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        // 差异表自身不带 bill_date，按需 left join 主表拿账单日期
        var query = from c in _db.ReconciliationChecks
                    join b in _db.WechatDailyBills on c.BillId equals b.Id into bg
                    from b in bg.DefaultIfEmpty()
                    select new { c, b };

        if (billDate.HasValue)
        {
            var d = billDate.Value.Date;
            query = query.Where(x => x.b != null && x.b.BillDate == d);
        }
        if (diffType.HasValue) query = query.Where(x => x.c.DiffType == diffType.Value);
        if (handleStatus.HasValue) query = query.Where(x => x.c.HandleStatus == handleStatus.Value);

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(x => x.c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReconCheckDto
            {
                Id = x.c.Id,
                BillDate = x.b == null ? string.Empty : x.b.BillDate.ToString("yyyy-MM-dd"),
                OrderId = x.c.OrderId,
                OutTradeNo = x.c.OutTradeNo ?? string.Empty,
                TransactionId = x.c.TransactionId,
                DiffType = x.c.DiffType,
                DiffTypeName = "",
                WechatAmount = x.c.WechatAmount,
                SystemAmount = x.c.SystemAmount,
                DiffAmount = x.c.WechatAmount - x.c.SystemAmount,
                WechatStatus = x.c.WechatStatus,
                SystemStatus = x.c.SystemStatus,
                SystemStatusName = "",
                HandleStatus = x.c.HandleStatus,
                HandleStatusName = "",
                Handler = x.c.Handler,
                Remark = x.c.Remark,
                CreateTime = x.c.CreateTime
            })
            .ToListAsync();

        // 名称字典在内存里翻译：EF 无法把 C# 的 switch 表达式翻译成 SQL
        foreach (var item in list)
        {
            item.DiffTypeName = DiffTypeNames.GetName(item.DiffType);
            item.HandleStatusName = CheckHandleStatusNames.GetName(item.HandleStatus);
            item.SystemStatusName = item.SystemStatus.HasValue ? PayOrderStatusNames.GetName(item.SystemStatus.Value) : "-";
        }

        return ApiResponse<PagedResult<ReconCheckDto>>.Success(
            PagedResult<ReconCheckDto>.Create(list, total, page, pageSize));
    }

    public async Task<ApiResponse<bool>> HandleDiffAsync(long id, string handler, string remark, int handleStatus)
    {
        var row = await _db.ReconciliationChecks.FirstOrDefaultAsync(c => c.Id == id);
        if (row == null)
        {
            return ApiResponse<bool>.Fail("差异记录不存在", 404);
        }

        row.HandleStatus = handleStatus;
        row.Handler = string.IsNullOrWhiteSpace(handler) ? null : handler.Trim();
        row.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, CheckHandleStatusNames.GetName(handleStatus));
    }

    // ============================================================
    // 内部：微信账单 API
    // ============================================================

    /// <summary>
    /// V3「申请交易账单」：GET /v3/bill/tradebill?bill_date=&amp;bill_type=&amp;tar_type=GZIP
    /// 返回 (download_url, hash_type, hash_value)。当日无账单时微信返回 204 No Content。
    /// </summary>
    private async Task<(string Url, string? HashType, string? HashValue)> ApplyTradeBillAsync(DateTime date, string billType)
    {
        var query = $"bill_date={date:yyyy-MM-dd}&bill_type={billType}&tar_type=GZIP";
        var path = $"/v3/bill/tradebill?{query}";
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

        // 204 / NO_CONTENT：微信当日无账单（或账单还没生成），按空账单处理
        if (resp.StatusCode == HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException($"微信返回「当日无账单」：{date:yyyy-MM-dd}（账单通常次日才生成）");
        }

        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"申请账单失败 HTTP {(int)resp.StatusCode}：{respBody}");
        }

        using var doc = JsonDocument.Parse(respBody);
        var root = doc.RootElement;

        var downloadUrl = root.TryGetProperty("download_url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException($"申请账单未返回 download_url：{respBody}");
        }

        var hashType = root.TryGetProperty("hash_type", out var ht) ? ht.GetString() : null;
        var hashValue = root.TryGetProperty("hash_value", out var hv) ? hv.GetString() : null;

        return (downloadUrl!, hashType, hashValue);
    }

    /// <summary>
    /// 下载账单文件内容。
    /// ⚠️ 该 download_url 是微信签名过的一次性地址，直接 GET 即可，**不需要**再带 Authorization 头
    ///    （带了反而会因为签名路径不匹配被拒）。这里用专用的长超时客户端 wechatbill。
    /// </summary>
    private async Task<byte[]> DownloadBillFileAsync(string downloadUrl)
    {
        using var client = _httpClientFactory.CreateClient("wechatbill");
        using var resp = await client.GetAsync(downloadUrl);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"下载账单文件失败 HTTP {(int)resp.StatusCode}：{errBody}");
        }

        return await resp.Content.ReadAsByteArrayAsync();
    }

    /// <summary>若字节流是 gzip（魔数 1F 8B）则解压，否则原样返回（微信在个别环境会返回明文 CSV）</summary>
    private static byte[] Decompress(byte[] data)
    {
        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return outMs.ToArray();
        }
        return data;
    }

    // ============================================================
    // 内部：CSV 解析（按列名定位，不依赖列序号）
    // ============================================================

    /// <summary>
    /// 解析微信账单 CSV。
    /// 微信账单的文本约定：
    ///   · 文件可能带 UTF-8 BOM；
    ///   · 每一行用反引号 ` 包裹，且尾部有一个冗余逗号；
    ///   · 前几行是标题/统计说明，真正的表头行包含「微信订单号」「商户订单号」等列名；
    ///   · 数据行首列是交易时间（yyyy-MM-dd HH:mm:ss）；
    ///   · 最后几行是汇总行（首列以「总」开头，如「总交易单数」）。
    /// 本方法按【列名】取值，而非写死下标 —— 微信调整列顺序/新增列都不会解析错。
    /// </summary>
    private List<BillRowDto> ParseBillCsv(byte[] fileBytes)
    {
        var rows = new List<BillRowDto>();

        // 去掉 UTF-8 BOM
        if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
        {
            fileBytes = fileBytes[3..];
        }

        var text = Encoding.UTF8.GetString(fileBytes);
        Dictionary<string, int>? header = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim('\r').Trim();
            if (line.Length == 0) continue;

            // 微信每行用反引号包裹，尾部还多一个逗号
            line = line.Trim('`').TrimEnd(',');
            var cols = SplitCsvLine(line);
            if (cols.Count == 0) continue;

            // ---- 找表头行 ----
            if (header == null)
            {
                if (cols.Count >= 8 && FindCol(cols, "商户订单号") >= 0 && FindCol(cols, "微信订单号") >= 0)
                {
                    header = new Dictionary<string, int>(StringComparer.Ordinal);
                    for (var i = 0; i < cols.Count; i++)
                    {
                        var name = cols[i].Trim();
                        if (name.Length > 0 && !header.ContainsKey(name)) header[name] = i;
                    }
                }
                continue;
            }

            // ---- 表头之后：数据行首列必须是交易时间 ----
            if (!DateTime.TryParseExact(cols[0].Trim(), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var tradeTime))
            {
                continue;   // 汇总行 / 说明行，跳过
            }

            rows.Add(new BillRowDto
            {
                TradeTime = tradeTime,
                TransactionId = GetCol(header, cols, "微信订单号"),
                OutTradeNo = GetCol(header, cols, "商户订单号"),
                TradeState = GetCol(header, cols, "交易状态"),
                // 「订单金额」是用户实付总额（不含优惠前的原价），是与系统金额对账的正确口径；
                // 「应结订单金额」是扣除优惠后商户实收，二者在有代金券时不相等，别选错。
                TotalFee = ParseAmount(GetCol(header, cols, "订单金额", "应结订单金额")),
                RefundFee = ParseAmount(GetCol(header, cols, "退款金额")),
                Poundage = ParseAmount(GetCol(header, cols, "手续费"))
            });
        }

        if (header == null && rows.Count == 0)
        {
            throw new InvalidOperationException("账单解析失败：未识别到表头行（可能文件格式变更或下载内容异常）");
        }

        return rows;
    }

    /// <summary>最小实现的 CSV 行切分（支持双引号包裹字段内的逗号与转义的双引号）</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') // "" 转义为一个 "
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        result.Add(sb.ToString());
        return result;
    }

    /// <summary>在表头行里按列名找下标（去空格后精确匹配）</summary>
    private static int FindCol(List<string> headerCols, string colName)
        => headerCols.FindIndex(c => string.Equals(c.Trim(), colName, StringComparison.Ordinal));

    /// <summary>按表头字典取值，支持多个候选列名依次回退；取不到返回空串</summary>
    private static string? GetCol(Dictionary<string, int> header, List<string> cols, params string[] names)
    {
        foreach (var name in names)
        {
            if (header.TryGetValue(name, out var idx) && idx < cols.Count)
            {
                var v = cols[idx].Trim();
                if (v.Length > 0) return v;
            }
        }
        return null;
    }

    /// <summary>
    /// 金额解析：微信账单里金额可能带货币符号或反引号，例如「¥100.00」「0.06」。
    /// 空串/横线一律视为 0，避免抛异常中断整份账单解析。
    /// </summary>
    private static decimal ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;

        var s = raw.Replace("¥", string.Empty).Replace("￥", string.Empty)
                   .Replace("`", string.Empty).Replace(",", string.Empty).Trim();

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    // ============================================================
    // 内部：勾稽辅助
    // ============================================================

    /// <summary>微信明细的聚合结果：代表行 + 是否存在退款</summary>
    private sealed class WechatAggregate
    {
        public WechatBillDetail Row { get; set; } = new();
        public bool HasRefund { get; set; }
    }

    /// <summary>
    /// 按 out_trade_no 聚合微信明细。
    /// ⚠️ 同一商户订单号在 ALL 账单里可能出现多行（支付行 + 退款行），直接 Dictionary 覆盖会被负金额污染。
    ///    这里取「订单金额最大」的那行作为代表行（即原始支付行），并单独标记是否发生过退款。
    /// </summary>
    private Dictionary<string, WechatAggregate> AggregateWechatRows(List<WechatBillDetail> wxRows)
    {
        var map = new Dictionary<string, WechatAggregate>(StringComparer.Ordinal);

        foreach (var row in wxRows)
        {
            if (string.IsNullOrWhiteSpace(row.OutTradeNo)) continue;
            var key = row.OutTradeNo;

            if (!map.TryGetValue(key, out var agg))
            {
                agg = new WechatAggregate { Row = row };
                map[key] = agg;
            }
            else if (row.TotalFee > agg.Row.TotalFee)
            {
                agg.Row = row;   // 金额更大的才是原始支付行
            }

            if (row.RefundFee > 0) agg.HasRefund = true;
        }

        return map;
    }

    /// <summary>微信侧该笔是否为「终态成功」（未退款/未撤销）</summary>
    private static bool IsFinalSuccess(string? tradeState)
        => string.Equals(tradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    /// <summary>构造一条差异记录（微信侧存在的场景共用）</summary>
    private static ReconciliationCheck BuildDiff(long billId, long orderId, string outTradeNo,
        WechatBillDetail wx, int diffType, decimal wechatAmount, decimal systemAmount)
        => new()
        {
            BillId = billId,
            OrderId = orderId,
            OutTradeNo = outTradeNo,
            TransactionId = wx.TransactionId,
            DiffType = diffType,
            WechatAmount = wechatAmount,
            SystemAmount = systemAmount,
            WechatStatus = wx.TradeState,
            SystemStatus = orderId > 0 ? (int)PayOrderStatus.Success : null,
            HandleStatus = (int)CheckHandleStatus.Pending,
            CreateTime = DateTime.Now
        };

    private async Task<List<ReconciliationCheck>> LoadDiffsAsync(long billId)
        => await _db.ReconciliationChecks.Where(c => c.BillId == billId).ToListAsync();

    /// <summary>汇总生成「今日对账报告」</summary>
    private async Task<ReconciliationReportDto> BuildReportAsync(WechatDailyBill bill, List<WechatBillDetail> wxRows,
        int sysCount, decimal sysAmount, List<ReconciliationCheck> diffs)
    {
        // 微信侧再内存聚合一次：这些统计无法用一个干净的单条 SQL 表达，数据量是「一天」，可接受
        var successRows = wxRows.Where(r => IsFinalSuccess(r.TradeState)).ToList();

        var report = new ReconciliationReportDto
        {
            BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
            BillId = bill.Id,

            WechatCount = wxRows.Count,
            WechatSuccessCount = successRows.Count,
            WechatAmount = wxRows.Sum(r => r.TotalFee),
            WechatSuccessAmount = successRows.Sum(r => r.TotalFee),
            WechatRefundAmount = wxRows.Sum(r => r.RefundFee),
            WechatFee = wxRows.Sum(r => r.Poundage),

            SystemCount = sysCount,
            SystemAmount = sysAmount,

            DiffCount = diffs.Count,
            WechatOnlyCount = diffs.Count(d => d.DiffType == (int)DiffType.WechatOnly),
            SystemOnlyCount = diffs.Count(d => d.DiffType == (int)DiffType.SystemOnly),
            AmountDiffCount = diffs.Count(d => d.DiffType == (int)DiffType.AmountDiff),
            StatusDiffCount = diffs.Count(d => d.DiffType == (int)DiffType.StatusDiff),
            DiffAmount = diffs.Sum(d => Math.Abs(d.WechatAmount - d.SystemAmount)),
            Balanced = diffs.Count == 0
        };

        report.Summary = report.Balanced
            ? $"{report.BillDate} 对账完成：账目一致。微信 {report.WechatCount} 笔 / ¥{report.WechatAmount:0.00}，系统支付成功 {report.SystemCount} 笔 / ¥{report.SystemAmount:0.00}"
            : $"{report.BillDate} 对账完成：发现 {report.DiffCount} 笔差异（长款 {report.WechatOnlyCount} / 短款 {report.SystemOnlyCount} / 金额不符 {report.AmountDiffCount} / 状态不符 {report.StatusDiffCount}），差异金额 ¥{report.DiffAmount:0.00}";

        report.Diffs = diffs.Take(200).Select(d => new ReconDiffItemDto
        {
            Id = d.Id,
            OutTradeNo = d.OutTradeNo ?? string.Empty,
            TransactionId = d.TransactionId,
            DiffType = d.DiffType,
            DiffTypeName = DiffTypeNames.GetName(d.DiffType),
            WechatAmount = d.WechatAmount,
            SystemAmount = d.SystemAmount,
            DiffAmount = d.WechatAmount - d.SystemAmount,
            WechatStatus = d.WechatStatus,
            SystemStatus = d.SystemStatus
        }).ToList();

        return await Task.FromResult(report);
    }

    // ============================================================
    // 内部：异常回写（绝不让异常冲出服务边界）
    // ============================================================

    /// <summary>读取账单主表的对账状态（收口 bill.ReconStatus 的读写，避免各处重复判空）</summary>
    private static int StatusOf(WechatDailyBill bill) => bill.ReconStatus;

    /// <summary>写入账单主表的对账状态（收口 bill.ReconStatus 的读写，避免各处重复判空）</summary>
    private static void SetStatus(WechatDailyBill bill, int status) => bill.ReconStatus = status;

    private async Task MarkBillErrorAsync(WechatDailyBill bill, string error)
    {
        try
        {
            SetStatus(bill, (int)BillReconStatus.Error);
            bill.ErrorMsg = error.Length > 1000 ? error[..1000] : error;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "回写账单异常状态失败（不影响主流程返回）");
        }
    }

    private async Task MarkBillErrorByDateAsync(DateTime date, string error)
    {
        try
        {
            var bill = await _db.WechatDailyBills.FirstOrDefaultAsync(b => b.BillDate == date.Date && b.BillType == "ALL");
            if (bill != null) await MarkBillErrorAsync(bill, error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "回写账单异常状态失败（按日期）");
        }
    }
}
