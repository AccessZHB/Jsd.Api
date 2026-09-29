using Jsd.Api.Models.Common;
using Jsd.Api.Models.Payment;

namespace Jsd.Api.Services;

/// <summary>
/// 微信支付 T+1 对账服务。
///
/// 两条主线：
///   1) DownloadDailyBillAsync  —— 向微信申请账单 → 下载（gzip）→ 解析 CSV → 明细落库；
///   2) AutoReconciliationAsync —— 系统支付成功流水 vs 微信账单明细 双向比对 → 差异落核查表 → 出报告。
///
/// 金额单位约定（很重要）：
///   · 微信账单 CSV 是「元」→ wechat_bill_detail.* 存 DECIMAL(18,2) 元；
///   · 系统 payment_order.amount 是「分」→ 比对时统一 /100m 换算成元；
///   · 比较用 ReconAmountTolerance.Yuan（半分）容差，避免浮点舍入噪音。
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// 下载指定日期的微信账单并解析入库（幂等）。
    /// </summary>
    /// <param name="billDate">账单日期（T 日；T+1 次日才能拉到，否则微信返回 204/错误）</param>
    /// <param name="billType">账单类型（见 BillType，默认 ALL）</param>
    /// <param name="force">本地已有该日明细时是否强制重新拉取（默认 false，避免触发微信接口频率限制）</param>
    Task<ApiResponse<BillDownloadDto>> DownloadDailyBillAsync(DateTime billDate, int billType = (int)BillType.All, bool force = false);

    /// <summary>
    /// 自动对账：比对系统支付成功记录与微信账单明细，产出差异 + 对账报告。
    /// 内部会确保账单已下载（本地没有则先调用 DownloadDailyBillAsync）。
    /// </summary>
    Task<ApiResponse<ReconciliationReportDto>> AutoReconciliationAsync(DateTime billDate, bool forceDownload = false);

    /// <summary>查询指定账单日的对账报告（不重跑对账，基于已落地数据汇总）</summary>
    Task<ApiResponse<ReconciliationReportDto>> GetReportAsync(DateTime billDate);

    /// <summary>分页查询对账差异核查列表</summary>
    Task<ApiResponse<PagedResult<ReconCheckDto>>> GetDiffListAsync(DateTime? billDate, int? diffType, int? handleStatus, int page, int pageSize);

    /// <summary>人工处理一笔差异（核销 / 挂账）</summary>
    Task<ApiResponse<bool>> HandleDiffAsync(long id, string handler, string remark, int handleStatus);
}
