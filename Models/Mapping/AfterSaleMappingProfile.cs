using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;

namespace Jsd.Api.Models.Mapping;

/// <summary>
/// 售后模块 AutoMapper 配置（仅映射实体与 DTO 的同名字段；
/// 客户名称/订单号/订单实付/状态文案等需联查或计算的展示字段由 Service 层手动补全）。
/// </summary>
public class AfterSaleMappingProfile : Profile
{
    public AfterSaleMappingProfile()
    {
        CreateMap<TrxRefund, RefundListDto>();
        CreateMap<TrxRefund, RefundDetailDto>();

        CreateMap<TrxReturn, ReturnListDto>();
        CreateMap<TrxReturn, ReturnDetailDto>();
    }
}
