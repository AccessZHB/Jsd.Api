using Jsd.Api.Entities;
using Jsd.Api.Models.Payment;
using Jsd.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 微信支付「待支付查单 / 补单 / 关单」定时任务（微信支付主动查单与补单模块）
///
/// 触发：sys_job 注册，cron = 0 0/5 * * * ?（每 5 分钟）。
/// 职责：
///   1) 扫描 payment_order 中 status=PAYING 且 未超时 的订单 → 逐个调微信查单 API；
///      · 微信 SUCCESS → 本地补单为 SUCCESS；
///      · 微信未支付且仍 未超时 → 保持不变（等下次扫描 / 回调）。
///   2) 扫描 payment_order 中 status=PAYING 且 已超时 的订单 → 再问一次微信；
///      · 微信 SUCCESS → 补单 SUCCESS；
///      · 微信未支付 → 本地关单 CLOSED（避免长期悬挂）。
/// 设计要点：
///   · 复用 PayService.QueryOrderFromWeChatAsync（与后台手动查单、回调补单同一套逻辑）；
///   · 单笔失败不影响整体扫描（逐单 try-catch）；
///   · 禁止并发（sys_job.concurrent=0），避免重复查单放大微信 API 压力。
/// </summary>
public class CheckPendingPaymentJob : IJob
{
    private readonly AppDbContext _db;
    private readonly IPayService _payService;
    private readonly ILogger<CheckPendingPaymentJob> _logger;

    public CheckPendingPaymentJob(AppDbContext db, IPayService payService, ILogger<CheckPendingPaymentJob> logger)
    {
        _db = db;
        _payService = payService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.Now;
        var fireKey = JobDataHelper.ResultKey(context);
        var batchSize = JobDataHelper.GetInt(context.JobDetail.JobDataMap, "batchSize", 200);

        var successCount = 0;
        var closedCount = 0;
        var failCount = 0;

        // ---------- 1. 未超时：主动查单 + 补单 ----------
        var pending = await _db.PaymentOrders
            .Where(p => p.Status == (int)PayOrderStatus.Paying && p.ExpireTime > now)
            .OrderBy(p => p.CreateTime)
            .Take(batchSize)
            .Select(p => p.OutTradeNo)
            .ToListAsync();

        foreach (var outTradeNo in pending)
        {
            try
            {
                var r = await _payService.QueryOrderFromWeChatAsync(outTradeNo);
                if (r.Code == 200 && r.Data != null)
                {
                    if (r.Data.Action == "SUCCESS") successCount++;
                    else if (r.Data.Action == "CLOSED") closedCount++;
                }
                else
                {
                    failCount++;
                    _logger.LogWarning("微信查单返回非成功，outTradeNo={OutTradeNo}，msg={Msg}", outTradeNo, r.Message);
                }
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "查单任务单笔异常，outTradeNo={OutTradeNo}", outTradeNo);
            }
        }

        // ---------- 2. 已超时：尝试关单 ----------
        var expired = await _db.PaymentOrders
            .Where(p => p.Status == (int)PayOrderStatus.Paying && p.ExpireTime <= now)
            .OrderBy(p => p.CreateTime)
            .Take(batchSize)
            .Select(p => p.OutTradeNo)
            .ToListAsync();

        foreach (var outTradeNo in expired)
        {
            try
            {
                var r = await _payService.QueryOrderFromWeChatAsync(outTradeNo);
                if (r.Code == 200 && r.Data != null)
                {
                    if (r.Data.Action == "CLOSED") closedCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "关单任务单笔异常，outTradeNo={OutTradeNo}", outTradeNo);
            }
        }

        var msg = $"微信查单任务完成：查单 {pending.Count} 笔（未超时），关单扫描 {expired.Count} 笔（已超时）；" +
                  $"补单成功 {successCount}，关单 {closedCount}，失败 {failCount}";
        _logger.LogInformation("微信支付查单任务：{Message}", msg);
        JobResultBuffer.Push(fireKey, msg);
    }
}
