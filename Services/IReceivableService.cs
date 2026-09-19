using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;

namespace Jsd.Api.Services;

/// <summary>
/// 应收账款 服务接口（对应菜单 321：/finance/receivable）
/// </summary>
public interface IReceivableService
{
    /// <summary>
    /// 分页查询应收账款列表。
    /// 支持按 客户名称 / 订单号 / 结算状态 / 订单日期范围 筛选。
    /// </summary>
    Task<PagedResult<ReceivableListDto>> GetListAsync(ReceivablePageQuery query);

    /// <summary>
    /// 获取某笔应收的详情，并返回其关联的核销记录（Join mkt_payment_item）。
    /// </summary>
    Task<ReceivableDetailDto?> GetDetailAsync(long id);

    /// <summary>
    /// 【关键】从订单同步生成应收账款。
    /// 触发时机：订单【发货】且 trx_order.is_credit = 1（信用订单）。
    /// 幂等：同一订单只会生成一条应收记录，重复调用返回已有记录ID。
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <returns>应收记录ID；非信用订单返回 0</returns>
    Task<long> SyncFromOrderAsync(long orderId);
}
