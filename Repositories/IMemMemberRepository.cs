using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 会员/客户 仓储接口。
/// 继承泛型 IRepository&lt;MemMember&gt;，直接复用 GetByIdAsync / AddAsync / SaveChangesAsync 等基础方法；
/// 仅补充本模块特有的查询。
/// </summary>
public interface IMemMemberRepository : IRepository<MemMember>
{
    /// <summary>分页查询客户列表（关键词/等级/状态/注册时间范围 筛选，自动过滤逻辑删除）</summary>
    Task<(List<MemMember> Items, int Total)> GetPagedListAsync(
        string? keyword, int? level, int? status, DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>根据主键查询（带跟踪 + 过滤逻辑删除，供编辑/更新使用）</summary>
    Task<MemMember?> GetByIdTrackedAsync(long id);

    /// <summary>根据会员编号查询（唯一）</summary>
    Task<MemMember?> GetByMemberNoAsync(string memberNo);

    /// <summary>根据登录账号查询（唯一，用于新增/编辑时账号重复校验）</summary>
    Task<MemMember?> GetByUserNameAsync(string userName);

    /// <summary>生成不重复的会员编号（M + yyyyMMddHHmmss + 4位随机数，重试 5 次）</summary>
    Task<string> GenerateMemberNoAsync();

    /// <summary>
    /// 是否存在"未完成（未发货）订单"：用于删除前校验。
    /// 口径：trx_order.buyer_id == 会员ID 且 order_status 属于 {0-待付款, 1-待发货} 且未逻辑删除。
    /// </summary>
    Task<bool> HasUnfinishedOrderAsync(long memberId);

    /// <summary>分页查询某客户的订单历史（跨表 trx_order，按创建时间倒序）</summary>
    Task<(List<TrxOrder> Items, int Total)> GetOrderHistoryAsync(long memberId, int page, int pageSize);
}
