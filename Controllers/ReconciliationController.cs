using Jsd.Api.Models.Common;
using Jsd.Api.Models.Payment;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 微信支付 T+1 对账 接口
///
/// 说明：本模块与支付模块（api/pay）同源配置（appsettings.json 的 WeChatPay 节），
/// 但与既有 Finance 模块的 PaymentCallbackController 完全解耦，互不干扰。
/// 全部接口需登录鉴权（对账涉及资金数据，不允许匿名访问）。
/// </summary>
[ApiController]
[Route("api/reconciliation")]
[Authorize]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;

    public ReconciliationController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    /// <summary>
    /// 手动下载指定日期的微信账单并解析入库（T+1：当日账单次日才可用）。
    /// GET /api/reconciliation/download?billDate=2026-09-27&amp;billType=1&amp;force=false
    /// </summary>
    [HttpGet("download")]
    public async Task<ApiResponse<BillDownloadDto>> Download([FromQuery] DateTime? billDate,
        [FromQuery] int billType = (int)BillType.All, [FromQuery] bool force = false)
    {
        try
        {
            // 与 run 保持一致的兜底：不传账单日时默认取【昨天】（T+1 账单）
            var date = billDate ?? DateTime.Today.AddDays(-1);
            return await _reconciliationService.DownloadDailyBillAsync(date, billType, force);
        }
        catch (Exception ex)
        {
            return ApiResponse<BillDownloadDto>.Fail($"下载失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 手动触发自动对账（不传 billDate 时默认对【昨天】）。
    /// 内部会先确保账单已下载（本地没有则自动补拉）。
    /// GET /api/reconciliation/run?billDate=2026-09-27&amp;forceDownload=false
    /// </summary>
    [HttpGet("run")]
    public async Task<ApiResponse<ReconciliationReportDto>> Run([FromQuery] DateTime? billDate,
        [FromQuery] bool forceDownload = false)
    {
        try
        {
            var date = billDate ?? DateTime.Today.AddDays(-1);
            return await _reconciliationService.AutoReconciliationAsync(date, forceDownload);
        }
        catch (Exception ex)
        {
            return ApiResponse<ReconciliationReportDto>.Fail($"对账失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 查询指定账单日的对账报告（不重跑对账，读已落地数据）。
    /// 不传 billDate 时默认查询【昨天】的报告。
    /// GET /api/reconciliation/report?billDate=2026-09-27
    /// </summary>
    [HttpGet("report")]
    public async Task<ApiResponse<ReconciliationReportDto>> Report([FromQuery] DateTime? billDate)
    {
        try
        {
            // 修复：原签名为非空 DateTime，未传参会得到 0001-01-01，
            // 导致提示"尚未账单数据：0001-01-01"。此处与 run/download 对齐，默认昨天。
            var date = billDate ?? DateTime.Today.AddDays(-1);
            return await _reconciliationService.GetReportAsync(date);
        }
        catch (Exception ex)
        {
            return ApiResponse<ReconciliationReportDto>.Fail($"查询失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 分页查询对账差异核查列表。
    /// GET /api/reconciliation/diffs?billDate=&amp;diffType=&amp;handleStatus=&amp;page=1&amp;pageSize=20
    /// </summary>
    [HttpGet("diffs")]
    public async Task<ApiResponse<PagedResult<ReconCheckDto>>> Diffs([FromQuery] DateTime? billDate,
        [FromQuery] int? diffType, [FromQuery] int? handleStatus,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            return await _reconciliationService.GetDiffListAsync(billDate, diffType, handleStatus, page, pageSize);
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ReconCheckDto>>.Fail($"查询失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 人工核销一笔差异（标记已处理 / 挂账，并记录处理人与备注）。
    /// PUT /api/reconciliation/diff/{id}
    /// </summary>
    [HttpPut("diff/{id:long}")]
    public async Task<ApiResponse<bool>> HandleDiff([FromRoute] long id, [FromBody] HandleDiffRequest request)
    {
        try
        {
            return await _reconciliationService.HandleDiffAsync(id, request.Handler, request.Remark, request.HandleStatus);
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"处理失败：{ex.Message}");
        }
    }
}

/// <summary>差异核销请求体</summary>
public class HandleDiffRequest
{
    /// <summary>处理状态（见 CheckHandleStatus：0待处理 1已处理 2挂账）</summary>
    public int HandleStatus { get; set; }

    /// <summary>处理人</summary>
    public string Handler { get; set; } = string.Empty;

    /// <summary>处理备注</summary>
    public string Remark { get; set; } = string.Empty;
}
