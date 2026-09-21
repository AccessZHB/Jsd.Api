using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 采购入库（以采定入）接口（采购管理模块）
/// 路由前缀：/api/purchase/inbound
///
/// 两阶段：创建(Create) 生成"待确认"入库单；确认(Confirm) 才增库存、写日志、生成应付、推进订单状态。
/// </summary>
[ApiController]
[Route("api/purchase/inbound")]
[Authorize]
public class InboundController : ControllerBase
{
    private readonly IInboundService _inboundService;

    public InboundController(IInboundService inboundService)
    {
        _inboundService = inboundService;
    }

    /// <summary>分页查询入库单列表</summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<InboundListDto>>> GetPage([FromQuery] InboundQueryDto query)
    {
        return await _inboundService.GetPagedListAsync(query);
    }

    /// <summary>入库单详情（主表 + 明细，明细带物料名/采购单价）</summary>
    [HttpGet("{id:long}/detail")]
    public async Task<ApiResponse<InboundDetailDto>> GetDetail(long id)
    {
        return await _inboundService.GetDetailAsync(id);
    }

    /// <summary>按采购订单查询其下所有入库单</summary>
    [HttpGet("by-order/{orderId:long}")]
    public async Task<ApiResponse<List<InboundListDto>>> GetByOrder(long orderId)
    {
        return await _inboundService.GetByOrderAsync(orderId);
    }

    /// <summary>创建入库单（以采定入核心入口）</summary>
    [HttpPost("create")]
    public async Task<ApiResponse<object>> Create([FromBody] CreateInboundDto dto)
    {
        try
        {
            return await _inboundService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>确认入库（增库存 + 写日志 + 生成应付 + 推进订单状态）</summary>
    [HttpPost("{id:long}/confirm")]
    public async Task<ApiResponse<object>> Confirm(long id)
    {
        try
        {
            return await _inboundService.ConfirmAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
