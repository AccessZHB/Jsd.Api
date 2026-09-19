using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Finance;

namespace Jsd.Api.Mapping;

/// <summary>
/// 财务结算模块的 AutoMapper 映射配置
/// （AddAutoMapper 会自动扫描程序集中所有 Profile，无需手动注册）
///
/// 约定：
///   - 派生展示字段（状态中文名、账龄、是否逾期）由 Profile 统一计算，Service 不再重复处理；
///   - 联查补全字段（订单号、客户名称）在实体上不存在，一律 Ignore，由 Service 手工赋值。
/// </summary>
public class FinanceMappingProfile : Profile
{
    public FinanceMappingProfile()
    {
        // ---------- 应收账款 trx_receivable ----------
        CreateMap<TrxReceivable, ReceivableListDto>()
            // 状态中文名（数据库原生状态）
            .ForMember(d => d.StatusName, o => o.MapFrom(s => ReceivableStatus.GetName(s.Status)))
            // 结算展示态（含由金额推导的"部分结清"）
            .ForMember(d => d.SettleName,
                       o => o.MapFrom(s => ReceivableStatus.GetSettleName(s.PaidAmount, s.UnpaidAmount, s.Status)))
            // 是否逾期：已过到期日且仍有未收金额
            .ForMember(d => d.IsOverdue,
                       o => o.MapFrom(s => s.DueDate.HasValue
                                        && s.DueDate.Value < DateTime.Today
                                        && s.UnpaidAmount > 0))
            // 账龄天数（距订单日期的自然日）
            .ForMember(d => d.AgingDays,
                       o => o.MapFrom(s => (int)(DateTime.Today - s.OrderDate).TotalDays))
            // 以下两个字段来自联查（trx_order / mem_member），由 Service 赋值
            .ForMember(d => d.OrderNo, o => o.Ignore())
            .ForMember(d => d.MemberName, o => o.Ignore());

        // 详情继承列表的映射规则（WriteOffs 由 Service 单独组装）
        CreateMap<TrxReceivable, ReceivableDetailDto>()
            .IncludeBase<TrxReceivable, ReceivableListDto>();

        // ---------- 收款记录 mkt_payment ----------
        CreateMap<MktPayment, PaymentListDto>()
            .ForMember(d => d.PaymentMethodName, o => o.MapFrom(s => PaymentMethods.GetName(s.PaymentMethod)))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => PaymentStatus.GetName(s.Status)))
            // 客户名称来自 mem_member 联查，由 Service 赋值
            .ForMember(d => d.MemberName, o => o.Ignore());

        CreateMap<MktPayment, PaymentDetailDto>()
            .IncludeBase<MktPayment, PaymentListDto>();

        // ---------- 支付日志 trx_payment_log ----------
        CreateMap<TrxPaymentLog, PaymentLogDto>()
            .ForMember(d => d.PaymentTypeName, o => o.MapFrom(s => PayLogTypes.GetName(s.PaymentType)))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => PayLogStatus.GetName(s.Status)));
    }
}
