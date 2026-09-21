using System.Security.Cryptography;
using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 退货单 仓储实现
/// </summary>
public class ReturnRepository : Repository<TrxReturn>, IReturnRepository
{
    public ReturnRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<TrxReturn> Items, int Total)> GetPagedListAsync(ReturnQueryDto q)
    {
        var query = Db.TrxReturns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.ReturnNo))
        {
            var kw = q.ReturnNo.Trim();
            query = query.Where(r => r.ReturnNo.Contains(kw));
        }

        // 订单号（模糊）：退货单冗余存了 order_no？不，退货单无 order_no 字段，
        // 故通过 EXISTS 子查询关联 trx_order 做筛选（避免 join 分页错乱）。
        if (!string.IsNullOrWhiteSpace(q.OrderNo))
        {
            var kw = q.OrderNo.Trim();
            query = query.Where(r => Db.TrxOrders.Any(o => o.Id == r.OrderId && o.OrderNo.Contains(kw)));
        }

        // 客户名称（模糊）：通过 EXISTS 子查询关联 mem_member
        if (!string.IsNullOrWhiteSpace(q.MemberName))
        {
            var kw = q.MemberName.Trim();
            query = query.Where(r => Db.MemMembers.Any(m => m.Id == r.MemberId && m.Name.Contains(kw)));
        }

        // 关联退款单号（模糊）：通过 EXISTS 子查询关联 trx_refund
        if (!string.IsNullOrWhiteSpace(q.RefundNo))
        {
            var kw = q.RefundNo.Trim();
            query = query.Where(r => Db.TrxRefunds.Any(f => f.Id == r.RefundId && f.RefundNo.Contains(kw)));
        }

        if (q.MemberId.HasValue)
        {
            query = query.Where(r => r.MemberId == q.MemberId.Value);
        }

        if (q.RefundId.HasValue)
        {
            query = query.Where(r => r.RefundId == q.RefundId.Value);
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

    public async Task<TrxReturn?> GetByIdTrackedAsync(long id)
    {
        return await Db.TrxReturns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<TrxReturn>> GetByRefundIdAsync(long refundId)
    {
        return await Db.TrxReturns.AsNoTracking()
            .Where(r => r.RefundId == refundId)
            .OrderByDescending(r => r.Id)
            .ToListAsync();
    }

    public async Task<bool> ExistsReturnNoAsync(string returnNo)
    {
        return await Db.TrxReturns.AsNoTracking().AnyAsync(r => r.ReturnNo == returnNo);
    }

    public string GenerateReturnNo()
    {
        var rand = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"TH{DateTime.Now:yyyyMMddHHmmss}{rand}";
    }
}
