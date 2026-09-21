using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 售后日志接口（售后管理模块）
/// 路由前缀：/api/after-sale/log
/// 提供"按退款单 / 按退货单"的全链路操作日志查询。
/// </summary>
[ApiController]
[Route("api/after-sale/log")]
[Authorize]
public class AfterSaleLogController : ControllerBase
{
    private readonly IAfterSaleLogService _logService;

    public AfterSaleLogController(IAfterSaleLogService logService)
    {
        _logService = logService;
    }

    /// <summary>查询某退款单的全链路操作日志</summary>
    [HttpGet("by-refund/{refundId:long}")]
    public async Task<ApiResponse<List<RefundLogDto>>> GetByRefund(long refundId)
    {
        return await _logService.GetByRefundAsync(refundId);
    }

    /// <summary>查询某退货单的操作日志</summary>
    [HttpGet("by-return/{returnId:long}")]
    public async Task<ApiResponse<List<RefundLogDto>>> GetByReturn(long returnId)
    {
        return await _logService.GetByReturnAsync(returnId);
    }
}
