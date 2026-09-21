using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 应付账款（采购付款）接口（采购管理模块）
/// 路由前缀：/api/purchase/payable
///
/// 应付款由入库确认时自动生成；本接口负责查询与线下付款核销。
/// </summary>
[ApiController]
[Route("api/purchase/payable")]
[Authorize]
public class PayableController : ControllerBase
{
    private readonly IPayableService _payableService;

    public PayableController(IPayableService payableService)
    {
        _payableService = payableService;
    }

    /// <summary>分页查询应付账款列表</summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<PayableListDto>>> GetPage([FromQuery] PayableQueryDto query)
    {
        return await _payableService.GetPagedListAsync(query);
    }

    /// <summary>应付账款详情（含关联采购订单号）</summary>
    [HttpGet("{id:long}/detail")]
    public async Task<ApiResponse<PayableDetailDto>> GetDetail(long id)
    {
        return await _payableService.GetDetailAsync(id);
    }

    /// <summary>按供应商查询应付账款</summary>
    [HttpGet("by-supplier/{supplierId:long}")]
    public async Task<ApiResponse<List<PayableListDto>>> GetBySupplier(long supplierId, [FromQuery] int? status)
    {
        return await _payableService.GetBySupplierAsync(supplierId, status);
    }

    /// <summary>付款核销（paid += 金额，unpaid -= 金额，状态 0/1/2）</summary>
    [HttpPost("{id:long}/pay")]
    public async Task<ApiResponse<object>> Pay(long id, [FromBody] PayPaymentDto dto)
    {
        try
        {
            return await _payableService.PayAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
