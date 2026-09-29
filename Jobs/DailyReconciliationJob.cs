using Jsd.Api.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>
/// 微信支付 T+1 自动对账定时任务
///
/// 触发：sys_job 注册，cron = 0 0 3 * * ?（每天凌晨 03:00）。
/// ⚠️ 为什么是凌晨 3 点 + 拉【昨日】：
///   微信当日账单次日才生成（官方口径约 T+1 上午），凌晨 3 点跑昨日账单，
///   既保证数据已完整（当日交易已结算到自然日结束），又避开业务高峰。
///
/// 职责：
///   1) 拉昨日微信账单（DownloadDailyBillAsync 内部幂等，已入库则跳过下载）；
///   2) 与本地 payment_order 支付成功流水双向勾稽，差异写 reconciliation_check；
///   3) 对账结论写回 wechat_daily_bill + 推 JobResultBuffer（任务执行记录页可见）。
///
/// 失败处理：对账失败不改变任何业务数据，只把 wechat_daily_bill 标 3(异常)，
/// 由当天人工重试或次日任务兜底，绝不中断调度器。
/// </summary>
public class DailyReconciliationJob : IJob
{
    private readonly IReconciliationService _reconciliationService;
    private readonly ILogger<DailyReconciliationJob> _logger;

    public DailyReconciliationJob(IReconciliationService reconciliationService, ILogger<DailyReconciliationJob> logger)
    {
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var fireKey = JobDataHelper.ResultKey(context);

        // 默认对账「昨天」；job_params 里配 billDate=yyyy-MM-dd 可临时对指定日期补跑
        var target = DateTime.Today.AddDays(-1);
        var billDateText = JobDataHelper.GetString(context.JobDetail.JobDataMap, "billDate");
        if (!string.IsNullOrWhiteSpace(billDateText)
            && DateTime.TryParse(billDateText, out var specified))
        {
            target = specified.Date;
        }

        var forceText = JobDataHelper.GetString(context.JobDetail.JobDataMap, "forceDownload");
        var force = JobDataHelper.GetBool(context.JobDetail.JobDataMap, "forceDownload",
            string.Equals(forceText, "true", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("微信T+1对账任务开始，账单日期={BillDate}", target.ToString("yyyy-MM-dd"));

        try
        {
            var result = await _reconciliationService.AutoReconciliationAsync(target, force);

            if (result.Code == 200 && result.Data != null)
            {
                var r = result.Data;
                var msg = $"{r.Summary}\n" +
                          $"微信 {r.WechatCount} 笔 / ¥{r.WechatAmount:0.00}（成功 {r.WechatSuccessCount} 笔 / ¥{r.WechatSuccessAmount:0.00}）；" +
                          $"系统支付成功 {r.SystemCount} 笔 / ¥{r.SystemAmount:0.00}；" +
                          $"手续费合计 ¥{r.WechatFee:0.00}，退款合计 ¥{r.WechatRefundAmount:0.00}";

                _logger.LogInformation("微信T+1对账任务完成：{Summary}", r.Summary);
                JobResultBuffer.Push(fireKey, msg);
            }
            else
            {
                var msg = $"T+1 对账失败（{target:yyyy-MM-dd}）：{result.Message}";
                _logger.LogWarning(msg);
                JobResultBuffer.Push(fireKey, msg);
            }
        }
        catch (Exception ex)
        {
            // 兜底：任务自身异常不得抛给 Quartz（否则触发重试风暴）
            _logger.LogError(ex, "微信T+1对账任务异常，账单日期={BillDate}", target.ToString("yyyy-MM-dd"));
            JobResultBuffer.Push(fireKey, $"T+1 对账异常：{ex.Message}");
        }
    }
}
