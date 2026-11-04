using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 购物车仓储（mem_cart）
/// 所有读写都必须带 member_id，从仓储层就杜绝越权访问他人购物车。
/// </summary>
public interface ICartRepository : IRepository<MemCart>
{
    /// <summary>取某会员全部购物车行（按加入时间倒序）</summary>
    Task<List<MemCart>> GetListByMemberAsync(long memberId);

    /// <summary>按「行ID + 会员ID」取购物车行（归属校验）</summary>
    Task<MemCart?> GetByIdAsync(long id, long memberId);

    /// <summary>按唯一键取购物车行（合并判定的核心查询）</summary>
    Task<MemCart?> FindByCartKeyAsync(string cartKey);

    /// <summary>清空某会员购物车，返回删除行数</summary>
    Task<int> DeleteByMemberAsync(long memberId);

    /// <summary>
    /// 批量设置勾选状态：id 为空时作用于该会员全部行（全选/全不选），
    /// 否则仅作用于指定行。返回影响行数。
    /// </summary>
    Task<int> SetSelectedAsync(long memberId, bool selected, long? id = null);
}
