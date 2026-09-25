using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Models.Mapping;

/// <summary>
/// 价格策略与会员余额模块 AutoMapper 配置。
/// 说明：仅映射实体与 DTO 的同名字段；状态文案、会员名称、策略名称等
///       需要联查或计算的展示字段由 Service 层手动补全（与售后/财务模块口径一致）。
/// </summary>
public class PriceMappingProfile : Profile
{
    public PriceMappingProfile()
    {
        // ---------- 客户等级 ----------
        CreateMap<MemMemberLevel, MemberLevelDto>();

        // ---------- 价格策略 ----------
        CreateMap<PriceStrategy, PriceStrategyDto>();

        // ---------- 价格规则 ----------
        CreateMap<PriceRule, PriceRuleDto>();
        CreateMap<PriceRuleItem, PriceRuleItemDto>();

        // ---------- 价格变更日志 ----------
        CreateMap<PriceChangeLog, PriceChangeLogDto>();

        // ---------- 订单价格快照 ----------
        CreateMap<OrderPriceSnapshot, OrderPriceSnapshotDto>();

        // ---------- 余额流水 ----------
        CreateMap<MktBalanceLog, BalanceLogDto>();
    }
}
