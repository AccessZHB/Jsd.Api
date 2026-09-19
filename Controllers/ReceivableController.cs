using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 应收账款接口（对应菜单 321：/finance/receivable）
///
/// 数据来源：信用订单（trx_order.is_credit=1）【发货】时自动生成，
/// 由 ReceivableService.SyncFromOrderAsync 统一写入 trx_receivable。
/// </summary>
[ApiController]
[Route("api/receivable")]
[Authorize]
public class ReceivableController : ControllerBase
{
    private readonly IReceivableService _receivableService;

    public ReceivableController(IReceivableService receivableService)
    {
        _receivableService = receivableService;
    }

    /// <summary>
    /// 分页查询应收账款列表
    /// 示例：GET /api/receivable/page?page=1&amp;pageSize=10&amp;memberName=张&amp;orderNo=ORD&amp;status=0
    /// </summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<ReceivableListDto>>> GetPage([FromQuery] ReceivablePageQuery query)
    {
        var result = await _receivableService.GetListAsync(query);
        return ApiResponse<PagedResult<ReceivableListDto>>.Success(result);
    }

    /// <summary>
    /// 应收账款详情（含该笔应收的核销记录）
    /// 示例：GET /api/receivable/1/detail
    /// </summary>
    [HttpGet("{id}/detail")]
    public async Task<ApiResponse<ReceivableDetailDto>> GetDetail(long id)
    {
        var dto = await _receivableService.GetDetailAsync(id);
        if (dto == null)
        {
            return ApiResponse<ReceivableDetailDto>.Fail("应收账款不存在");
        }
        return ApiResponse<ReceivableDetailDto>.Success(dto);
    }

    /// <summary>
    /// 【内部】从订单同步生成应收账款。
    /// 触发条件：订单已发货且 trx_order.is_credit = 1。
    /// 幂等：同一订单重复调用不会重复生成。
    /// 示例：POST /api/receivable/sync/1001
    /// </summary>
    [HttpPost("sync/{orderId}")]
    public async Task<ApiResponse<object>> SyncFromOrder(long orderId)
    {
        try
        {
            var receivableId = await _receivableService.SyncFromOrderAsync(orderId);
            var msg = receivableId > 0 ? "应收账款已生成" : "非信用订单，未生成应收账款";
            return ApiResponse<object>.Success(new { receivableId }, msg);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
