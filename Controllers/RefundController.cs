using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 退款申请接口（售后管理模块）
/// 路由前缀：/api/after-sale/refund
///
/// 状态流转：0-待审核 ──审核──&gt; 1-待退款 ──执行退款──&gt; 2-已退款（终态）；
///          0 ──驳回──&gt; 3-已驳回。
/// 退款类型：1-仅退款 2-退货退款（审核通过自动生成退货单，需仓库收货后退款）。
/// </summary>
[ApiController]
[Route("api/after-sale/refund")]
[Authorize]
public class RefundController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    /// <summary>提交退款申请</summary>
    [HttpPost]
    public async Task<ApiResponse<RefundCreateResult>> Create([FromBody] CreateRefundDto dto)
    {
        try
        {
            return await _refundService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<RefundCreateResult>.Fail(ex.Message);
        }
    }

    /// <summary>退款列表分页查询</summary>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<RefundListDto>>> GetPage([FromQuery] RefundQueryDto query)
    {
        return await _refundService.GetPagedListAsync(query);
    }

    /// <summary>退款详情（含关联退货单与全链路日志）</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<RefundDetailDto>> GetDetail(long id)
    {
        return await _refundService.GetDetailAsync(id);
    }

    /// <summary>审核退款（通过/驳回）</summary>
    [HttpPut("{id:long}/approve")]
    public async Task<ApiResponse<object>> Approve(long id, [FromBody] ApproveRefundDto dto)
    {
        try
        {
            return await _refundService.ApproveAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>执行退款（财务打款，幂等）</summary>
    [HttpPost("{id:long}/execute")]
    public async Task<ApiResponse<object>> Execute(long id)
    {
        try
        {
            return await _refundService.ExecuteAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
