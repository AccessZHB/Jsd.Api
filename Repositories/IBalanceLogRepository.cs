using Jsd.Api.Entities;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Repositories;

/// <summary>
/// 余额流水 仓储接口（mkt_balance_log，余额真值台账）
/// </summary>
public interface IBalanceLogRepository : IRepository<MktBalanceLog>
{
    /// <summary>分页查询余额流水（会员 / 变动类型 / 关联业务 / 时间范围筛选）</summary>
    Task<(List<MktBalanceLog> Items, int Total)> GetPagedListAsync(BalanceLogQueryDto q);

    /// <summary>
    /// 按关联单据查询流水是否存在（用于冻结/扣减/退款的幂等判断）。
    /// relatedType 为数值枚举 BalanceRelatedType（0-无 1-订单 2-充值订单 3-退款单）。
    /// </summary>
    Task<MktBalanceLog?> GetByRelatedAsync(long memMemberId, int relatedType, long relatedId, int changeType);
}
