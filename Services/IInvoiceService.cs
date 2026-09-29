using Jsd.Api.Models.Common;
using Jsd.Api.Models.Invoice;

namespace Jsd.Api.Services;

/// <summary>
/// 发票与税务管理服务（发票与税务管理模块）
///
/// 业务主线：
///   1) 客户开票信息维护（customer_invoice_info）：多抬头、可设默认；
///   2) 开票申请（order_invoice.status=0 申请中）：订单支付成功后提交，抬头/税号做快照；
///      · 对接了第三方开票平台（百望云/航信等）→ 申请时即发起开票调用；
///      · 未对接（默认）→ 转财务人工开票，开票后由 IssueInvoiceAsync 回写发票代码/号码；
///   3) 作废 / 红冲（status=2 已作废）：申请中=撤销申请，已开票=红冲（须填蓝票号码+原因）。
/// </summary>
public interface IInvoiceService
{
    /// <summary>新增或修改客户开票信息（Id=0 新增；设为默认时自动取消该客户其它默认）</summary>
    Task<ApiResponse<long>> SaveCustomerInvoiceAsync(SaveCustomerInvoiceDto dto);

    /// <summary>查询某客户的开票信息列表（默认排最前）</summary>
    Task<ApiResponse<List<CustomerInvoiceInfoDto>>> GetCustomerInvoiceListAsync(long customerId);

    /// <summary>查询单条客户开票信息</summary>
    Task<ApiResponse<CustomerInvoiceInfoDto>> GetCustomerInvoiceAsync(long id);

    /// <summary>提交开票申请（订单支付成功后；状态置为「申请中」）</summary>
    Task<ApiResponse<ApplyInvoiceResult>> ApplyInvoiceAsync(ApplyInvoiceDto dto);

    /// <summary>作废 / 红冲开票记录（状态置为「已作废」并记录原因）</summary>
    Task<ApiResponse<CancelInvoiceResult>> CancelInvoiceAsync(long id, CancelInvoiceDto dto);

    /// <summary>财务回写票面信息（发票代码/号码/开票日期），状态置为「已开票」</summary>
    Task<ApiResponse<bool>> IssueInvoiceAsync(long id, IssueInvoiceDto dto);

    /// <summary>开票记录分页列表</summary>
    Task<ApiResponse<PagedResult<InvoiceListDto>>> GetPagedListAsync(InvoiceQueryDto query);

    /// <summary>开票记录详情（含订单实付金额，便于核对票面金额）</summary>
    Task<ApiResponse<InvoiceDetailDto>> GetDetailAsync(long id);
}
