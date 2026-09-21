using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 退货管理接口（售后管理模块）
/// 路由前缀：/api/after-sale/return
///
/// 状态流转：1-待退货 ──仓库收货──&gt; 3-仓库已收货 ──(退货退款)自动退款──&gt; 4-已退款；
///          全部拒收──&gt; 5-已拒绝。
/// </summary>
[ApiController]
[Route("api/after-sale/return")]
[Authorize]
public class ReturnController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    /// <summary>创建退货单（手动创建或退款审核自动生成）</summary>
    [HttpPost]
    public async Task<ApiResponse<ReturnCreateResult>> Create([FromBody] CreateReturnDto dto)
    {
        try
        {
            return await _returnService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ReturnCreateResult>.Fail(ex.Message);
        }
    }

    /// <summary>退货列表分页查询</summary>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<ReturnListDto>>> GetPage([FromQuery] ReturnQueryDto query)
    {
        return await _returnService.GetPagedListAsync(query);
    }

    /// <summary>退货详情（含明细）</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<ReturnDetailDto>> GetDetail(long id)
    {
        return await _returnService.GetDetailAsync(id);
    }

    /// <summary>仓库确认收货（库存回滚 + 自动触发退款）</summary>
    [HttpPut("{id:long}/receive")]
    public async Task<ApiResponse<object>> Receive(long id, [FromBody] ReceiveReturnDto dto)
    {
        try
        {
            return await _returnService.ReceiveAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>根据退款单查询关联退货单</summary>
    [HttpGet("by-refund/{refundId:long}")]
    public async Task<ApiResponse<List<ReturnListDto>>> GetByRefund(long refundId)
    {
        return await _returnService.GetByRefundAsync(refundId);
    }
}
