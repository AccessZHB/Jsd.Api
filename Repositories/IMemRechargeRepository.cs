using Jsd.Api.Entities;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Repositories;

/// <summary>
/// 充值订单 仓储接口（mem_recharge）
/// </summary>
public interface IMemRechargeRepository : IRepository<MemRecharge>
{
    /// <summary>充值单号是否已存在</summary>
    Task<bool> ExistsRechargeNoAsync(string rechargeNo);

    /// <summary>
    /// 【幂等键】按微信支付流水号查询充值单。
    /// 微信重复回调时据此判断是否已入账，严禁重复入账。
    /// </summary>
    Task<MemRecharge?> GetByTransactionIdAsync(string transactionId);

    /// <summary>按充值单号取可跟踪实体（回调入账用）</summary>
    Task<MemRecharge?> GetByRechargeNoTrackedAsync(string rechargeNo);

    /// <summary>生成充值单号：RC + yyyyMMddHHmmss + 4 位随机</summary>
    string GenerateRechargeNo();

    /// <summary>分页查询充值记录（支持单号/客户/渠道/状态/时间范围筛选）</summary>
    Task<(List<MemRecharge> Items, int Total)> GetPagedListAsync(RechargeQueryDto q);
}
