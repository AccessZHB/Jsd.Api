using Jsd.Api.Entities;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Repositories;

/// <summary>
/// 客户等级 仓储接口（mem_member_level）
/// </summary>
public interface IMemberLevelRepository : IRepository<MemMemberLevel>
{
    /// <summary>分页查询等级列表（支持状态、关键字筛选）</summary>
    Task<(List<MemMemberLevel> Items, int Total)> GetPagedListAsync(MemberLevelQueryDto q);

    /// <summary>等级编码是否已存在（用于唯一性校验；excludeId&gt;0 时排除自身）</summary>
    Task<bool> ExistsLevelCodeAsync(string levelCode, long excludeId = 0);

    /// <summary>是否存在引用该等级的启用中价格规则（停用前的业务校验）</summary>
    Task<bool> HasEnabledRulesAsync(long levelId);

    /// <summary>根据ID取可跟踪实体（更新用）</summary>
    Task<MemMemberLevel?> GetByIdTrackedAsync(long id);
}
