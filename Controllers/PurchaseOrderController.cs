using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 采购订单接口（采购管理模块）
/// 路由前缀：/api/purchase/order
///
/// 状态流转（本模块【无审核环节】）：
///   0-草稿 ──确认订单──&gt; 2-待入库 ──入库确认──&gt; 3-部分入库 / 4-已结清；
///   0 ──作废──&gt; 9-作废。
/// 注：pur_order 无 approver_id / approve_time 字段，确认动作只推进状态，不记录审批人。
/// </summary>
[ApiController]
[Route("api/purchase/order")]
[Authorize]
public class PurchaseOrderController : ControllerBase
{
    private readonly IPurchaseOrderService _orderService;

    public PurchaseOrderController(IPurchaseOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>分页查询采购订单列表</summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<PurchaseOrderListDto>>> GetPage([FromQuery] PurchaseOrderQueryDto query)
    {
        return await _orderService.GetPagedListAsync(query);
    }

    /// <summary>采购订单详情（含明细与已入库数量）</summary>
    [HttpGet("{id:long}/detail")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> GetDetail(long id)
    {
        return await _orderService.GetDetailAsync(id);
    }

    /// <summary>创建采购订单（草稿）</summary>
    [HttpPost("create")]
    public async Task<ApiResponse<PurchaseOrderCreateResult>> Create([FromBody] CreatePurchaseOrderDto dto)
    {
        try
        {
            return await _orderService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<PurchaseOrderCreateResult>.Fail(ex.Message);
        }
    }

    /// <summary>编辑采购订单（仅草稿）</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] UpdatePurchaseOrderDto dto)
    {
        try
        {
            return await _orderService.UpdateAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认订单（草稿 → 待入库）。
    /// 【本模块无审核环节】确认后订单即可开入库单，不记录审批人/审批时间。
    /// </summary>
    [HttpPost("{id:long}/confirm")]
    public async Task<ApiResponse<object>> Confirm(long id)
    {
        try
        {
            return await _orderService.ConfirmAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>作废采购订单（草稿 → 作废）</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<ApiResponse<object>> Cancel(long id)
    {
        try
        {
            return await _orderService.CancelAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>根据库存预警自动生成采购订单</summary>
    [HttpPost("auto-create")]
    public async Task<ApiResponse<List<PurchaseOrderCreateResult>>> AutoCreate([FromBody] AutoCreateOrderDto dto)
    {
        try
        {
            return await _orderService.AutoCreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<List<PurchaseOrderCreateResult>>.Fail(ex.Message);
        }
    }
}
