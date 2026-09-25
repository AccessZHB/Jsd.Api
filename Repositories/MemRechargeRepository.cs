using System.Security.Cryptography;
using Jsd.Api.Entities;
using Jsd.Api.Models.Price;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 充值订单 仓储实现（mem_recharge）
/// </summary>
public class MemRechargeRepository : Repository<MemRecharge>, IMemRechargeRepository
{
    public MemRechargeRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<bool> ExistsRechargeNoAsync(string rechargeNo)
    {
        return await Db.MemRecharges.AsNoTracking().AnyAsync(r => r.RechargeNo == rechargeNo);
    }

    public async Task<MemRecharge?> GetByTransactionIdAsync(string transactionId)
    {
        // AsNoTracking 仅用于"是否已入账"的判断；真正入账时按单号再取一次可跟踪实体
        return await Db.MemRecharges.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TransactionId == transactionId);
    }

    public async Task<MemRecharge?> GetByRechargeNoTrackedAsync(string rechargeNo)
    {
        return await Db.MemRecharges.FirstOrDefaultAsync(r => r.RechargeNo == rechargeNo);
    }

    public string GenerateRechargeNo()
    {
        var rand = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"RC{DateTime.Now:yyyyMMddHHmmss}{rand}";
    }

    public async Task<(List<MemRecharge> Items, int Total)> GetPagedListAsync(RechargeQueryDto q)
    {
        var query = Db.MemRecharges.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.RechargeNo))
        {
            var no = q.RechargeNo.Trim();
            query = query.Where(r => r.RechargeNo.Contains(no));
        }

        // 客户名称/编号模糊：EXISTS 子查询关联 mem_member（与项目其它列表口径一致，避免 join 分页错乱）
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(r =>
                Db.MemMembers.Any(m => m.Id == r.MemMemberId && (m.Name.Contains(kw) || m.MemberNo.Contains(kw))));
        }

        // 支付方式：DTO 传渠道码（WECHAT / OFFLINE），数据库列是数值 pay_type，需转换后比较
        if (!string.IsNullOrWhiteSpace(q.PayChannel))
        {
            var payType = PayTypeHelper.FromCode(q.PayChannel.Trim());
            query = query.Where(r => r.PayType == payType);
        }

        if (q.Status.HasValue)
        {
            query = query.Where(r => r.Status == q.Status.Value);
        }

        if (q.StartTime.HasValue)
        {
            query = query.Where(r => r.CreateTime >= q.StartTime.Value);
        }
        if (q.EndTime.HasValue)
        {
            query = query.Where(r => r.CreateTime < q.EndTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return (items, total);
    }
}
