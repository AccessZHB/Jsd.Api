using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 会员/客户 仓储实现
/// </summary>
public class MemMemberRepository : Repository<MemMember>, IMemMemberRepository
{
    public MemMemberRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询客户列表。
    /// 关键词命中：name / company_name / member_no（任一包含即匹配）。
    /// 统一过滤 is_deleted = 0。
    /// </summary>
    public async Task<(List<MemMember> Items, int Total)> GetPagedListAsync(
        string? keyword, int? level, int? status, DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        var query = Db.MemMembers.AsNoTracking().Where(m => m.IsDeleted == 0).AsQueryable();

        // 关键词：名称 / 公司 / 会员编号
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(m => m.Name.Contains(kw)
                                  || (m.CompanyName != null && m.CompanyName.Contains(kw))
                                  || m.MemberNo.Contains(kw));
        }

        // 等级筛选
        if (level.HasValue)
        {
            query = query.Where(m => m.Level == level.Value);
        }

        // 状态筛选
        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        // 注册时间范围（按创建时间；EndTime 包含当天，故 +1 天再比较）
        if (startTime.HasValue)
        {
            query = query.Where(m => m.CreateTime >= startTime.Value);
        }
        if (endTime.HasValue)
        {
            query = query.Where(m => m.CreateTime < endTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<MemMember?> GetByIdTrackedAsync(long id)
    {
        // 带跟踪：编辑时直接修改实体属性即可被 EF 感知
        return await Db.MemMembers.FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted == 0);
    }

    public async Task<MemMember?> GetByMemberNoAsync(string memberNo)
    {
        return await Db.MemMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.IsDeleted == 0);
    }

    public async Task<MemMember?> GetByUserNameAsync(string userName)
    {
        return await Db.MemMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserName == userName && m.IsDeleted == 0);
    }

    /// <summary>
    /// 生成唯一会员编号：M + yyyyMMddHHmmss(14) + 4位随机数。
    /// 与订单号同理，uk_member_no 唯一索引是最后一道防线，业务侧先查重再重试。
    /// </summary>
    public async Task<string> GenerateMemberNoAsync()
    {
        const string prefix = "M";
        for (var i = 0; i < 5; i++)
        {
            var no = $"{prefix}{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
            if (await Db.MemMembers.AsNoTracking().AllAsync(m => m.MemberNo != no))
            {
                return no;
            }
        }
        // 极端并发下兜底：时间戳 + GUID 前 6 位，保证唯一
        return $"{prefix}{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid().ToString("N")[..6]}";
    }

    /// <summary>
    /// 未完成（未发货）订单校验。
    /// 未发货 = order_status ∈ {0 待付款, 1 待发货}，即尚未进入"已发货"流转。
    /// </summary>
    public async Task<bool> HasUnfinishedOrderAsync(long memberId)
    {
        return await Db.TrxOrders.AsNoTracking()
            .AnyAsync(o => o.BuyerId == memberId
                        && o.IsDeleted == 0
                        && (o.OrderStatus == 0 || o.OrderStatus == 1));
    }

    /// <summary>
    /// 分页查询某客户的订单历史。
    /// trx_order.buyer_id 即会员 id；按创建时间倒序（最近订单在前）。
    /// </summary>
    public async Task<(List<TrxOrder> Items, int Total)> GetOrderHistoryAsync(long memberId, int page, int pageSize)
    {
        var query = Db.TrxOrders.AsNoTracking()
            .Where(o => o.BuyerId == memberId && o.IsDeleted == 0);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
