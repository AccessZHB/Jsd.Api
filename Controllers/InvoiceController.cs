using FluentValidation;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Invoice;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 发票与税务管理接口
/// 路由前缀：/api/invoice
///
/// 业务闭环：
///   客户维护开票抬头 → 订单支付成功 → 提交开票申请（申请中）
///   → 财务人工开票后回写发票代码/号码（已开票）【或对接开票平台时自动开票】
///   → 开错/退票时作废或红冲（已作废）。
/// </summary>
[ApiController]
[Route("api/invoice")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoiceController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    // ==================== 客户开票信息 ====================

    /// <summary>
    /// 新增 / 修改客户开票信息（Id=0 新增；IsDefault=1 时自动取消该客户其它默认抬头）
    /// POST /api/invoice/info
    /// </summary>
    [HttpPost("info")]
    public async Task<ApiResponse<long>> SaveCustomerInvoice([FromBody] SaveCustomerInvoiceDto dto)
    {
        try
        {
            return await _invoiceService.SaveCustomerInvoiceAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<long>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 查询某客户的开票信息列表（默认抬头排最前）
    /// GET /api/invoice/info/{customerId}
    /// </summary>
    [HttpGet("info/{customerId:long}")]
    public async Task<ApiResponse<List<CustomerInvoiceInfoDto>>> GetCustomerInvoiceList(long customerId)
    {
        return await _invoiceService.GetCustomerInvoiceListAsync(customerId);
    }

    /// <summary>
    /// 查询单条客户开票信息
    /// GET /api/invoice/info/detail/{id}
    /// </summary>
    [HttpGet("info/detail/{id:long}")]
    public async Task<ApiResponse<CustomerInvoiceInfoDto>> GetCustomerInvoice(long id)
    {
        return await _invoiceService.GetCustomerInvoiceAsync(id);
    }

    // ==================== 开票申请 / 作废 / 回写 ====================

    /// <summary>
    /// 提交开票申请（订单支付成功后）：写入开票记录，状态置为「申请中」。
    /// 已对接第三方开票平台则同步发起开票；未对接则转财务人工开票，开票后调用 issue 回写票号。
    /// POST /api/invoice/apply
    /// </summary>
    [HttpPost("apply")]
    public async Task<ApiResponse<ApplyInvoiceResult>> ApplyInvoice([FromBody] ApplyInvoiceDto dto)
    {
        try
        {
            return await _invoiceService.ApplyInvoiceAsync(dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 作废 / 红冲：状态置为「已作废」并记录原因。
    /// 规则：申请中 → 撤销申请；已开票 → 红冲（必须填【关联蓝票发票号码】）。
    /// PUT /api/invoice/{id}/cancel
    /// </summary>
    [HttpPut("{id:long}/cancel")]
    public async Task<ApiResponse<CancelInvoiceResult>> CancelInvoice(long id, [FromBody] CancelInvoiceDto dto)
    {
        try
        {
            return await _invoiceService.CancelInvoiceAsync(id, dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<CancelInvoiceResult>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<CancelInvoiceResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 财务回写票面信息（发票代码 / 发票号码 / 开票日期），状态置为「已开票」。
    /// PUT /api/invoice/{id}/issue
    /// </summary>
    [HttpPut("{id:long}/issue")]
    public async Task<ApiResponse<bool>> IssueInvoice(long id, [FromBody] IssueInvoiceDto dto)
    {
        try
        {
            return await _invoiceService.IssueInvoiceAsync(id, dto);
        }
        catch (ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
    }

    // ==================== 查询 ====================

    /// <summary>
    /// 开票记录分页列表（订单号 / 抬头 / 发票号 关键词，状态、类型、时间范围筛选）
    /// GET /api/invoice/list
    /// </summary>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<InvoiceListDto>>> GetList([FromQuery] InvoiceQueryDto query)
    {
        return await _invoiceService.GetPagedListAsync(query);
    }

    /// <summary>
    /// 开票记录详情（含订单实付金额，用于核对票面金额）
    /// GET /api/invoice/{id}
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<InvoiceDetailDto>> GetDetail(long id)
    {
        return await _invoiceService.GetDetailAsync(id);
    }
}
