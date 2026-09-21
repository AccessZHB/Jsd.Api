using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;

namespace Jsd.Api.Repositories;

/// <summary>
/// 退款申请 仓储接口（继承泛型仓储，复用通用能力；扩展分页/幂等/单号生成）
/// </summary>
public interface IRefundRepository : IRepository<TrxRefund>
{
    /// <summary>分页查询退款列表（支持订单号/退款单号/状态/申请时间范围筛选）</summary>
    Task<(List<TrxRefund> Items, int Total)> GetPagedListAsync(RefundQueryDto q);

    /// <summary>带跟踪查询（审核/执行退款时需要修改状态）</summary>
    Task<TrxRefund?> GetByIdTrackedAsync(long id);

    /// <summary>同一订单已退款（非驳回）金额合计，用于"未全额退款"校验</summary>
    Task<decimal> GetRefundedSumByOrderAsync(long orderId);

    /// <summary>退款单号是否已存在（唯一索引兜底前的预检）</summary>
    Task<bool> ExistsRefundNoAsync(string refundNo);

    /// <summary>生成退款单号候选（TK + yyyyMMddHHmmss + 4位随机；撞号由唯一索引兜底后重试）</summary>
    string GenerateRefundNo();
}
