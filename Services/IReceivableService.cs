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
    /// 【关键】从订单同步生成应收账款（订单创建后即调用）。
    /// 触发时机：订单创建（待付款）时；订单【发货】时兜底补齐。
    /// 幂等：同一订单只会生成一条应收记录，重复调用返回已有记录ID。
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <returns>应收记录ID；订单已关闭时返回 0</returns>
    Task<long> SyncFromOrderAsync(long orderId);

    /// <summary>
    /// 订单【支付成功】时结清其应收账款（paid=total、unpaid=0、状态=已结清）。
    /// 用于在线/余额支付：这笔钱不经收款单核销，必须直接结清应收，避免残留脏应收。
    /// 幂等：无应收单或已结清时安全返回。
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <returns>应收记录ID；无应收单返回 0</returns>
    Task<long> SettleByOrderAsync(long orderId);
}
