using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Invoice;

namespace Jsd.Api.Models.Mapping;

/// <summary>
/// 发票与税务管理模块 AutoMapper 配置。
/// 与售后/财务模块口径一致：只映射实体与 DTO 的同名字段；
/// 订单号、订单实付、状态/类型中文名等需联查或计算的展示字段由 Service 层手动补全。
/// </summary>
public class InvoiceMappingProfile : Profile
{
    public InvoiceMappingProfile()
    {
        // ---------- 客户开票信息 ----------
        CreateMap<CustomerInvoiceInfo, CustomerInvoiceInfoDto>();

        // ---------- 订单开票记录 ----------
        CreateMap<OrderInvoice, InvoiceListDto>();
        CreateMap<OrderInvoice, InvoiceDetailDto>();
    }
}
