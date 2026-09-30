using System.Security.Cryptography;
using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 退款申请 仓储实现
/// 列表查询采用"EXISTS 子查询筛选 + 分页取回 + 字典批量补全展示字段"策略
/// （与 ReceivableRepository 一致），避免 join 分页错乱与 N+1。
/// </summary>
public class RefundRepository : Repository<TrxRefund>, IRefundRepository
{
    public RefundRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<TrxRefund> Items, int Total)> GetPagedListAsync(RefundQueryDto q)
    {
        var query = Db.TrxRefunds.AsNoTracking().AsQueryable();

        // 订单号（模糊）：退款单冗余存了 order_no，直接查本表即可
        if (!string.IsNullOrWhiteSpace(q.OrderNo))
        {
            var kw = q.OrderNo.Trim();
            query = query.Where(r => r.OrderNo != null && r.OrderNo.Contains(kw));
        }

        // 退款单号（模糊）
        if (!string.IsNullOrWhiteSpace(q.RefundNo))
        {
            var kw = q.RefundNo.Trim();
            query = query.Where(r => r.RefundNo.Contains(kw));
        }

        // 客户名称（模糊）：通过 EXISTS 子查询关联 mem_member，避免 join 分页错乱
        if (!string.IsNullOrWhiteSpace(q.MemberName))
        {
            var kw = q.MemberName.Trim();
            query = query.Where(r => Db.MemMembers.Any(m => m.Id == r.MemberId && m.Name.Contains(kw)));
        }

        // 退款状态
        if (q.Status.HasValue)
        {
            query = query.Where(r => r.Status == q.Status.Value);
        }

        // 申请时间范围（含当天，故 end +1 天取开区间）
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

    public async Task<TrxRefund?> GetByIdTrackedAsync(long id)
    {
        return await Db.TrxRefunds.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<decimal> GetRefundedSumByOrderAsync(long orderId)
    {
        // 已占用额度 = 待审核(0) + 待退款(1) + 已退款(2)；驳回(3)不计入
        //
        // ⚠️ 缺陷修复：原实现只统计 已通过(1)+已退款(2)，漏掉了【待审核(0)】。
        // 后果：同一订单可反复提交"待审核"退款单，每笔都只与"已通过金额"比较，
        //       若审核人逐笔通过，累计退款将超过订单实付金额（资损风险）。
        //       例：订单实付 179，可先提交 5 笔 170 的待审核申请，全部通过后退款 850。
        return await Db.TrxRefunds.AsNoTracking()
            .Where(r => r.OrderId == orderId
                        && (r.Status == (int)RefundStatus.Pending
                            || r.Status == (int)RefundStatus.Approved
                            || r.Status == (int)RefundStatus.Refunded))
            .SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;
    }

    public async Task<bool> ExistsRefundNoAsync(string refundNo)
    {
        return await Db.TrxRefunds.AsNoTracking().AnyAsync(r => r.RefundNo == refundNo);
    }

    public string GenerateRefundNo()
    {
        // TK + yyyyMMddHHmmss + 4 位随机（撞号由数据库 uk_refund_no 唯一索引兜底，Service 重试）
        var rand = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"TK{DateTime.Now:yyyyMMddHHmmss}{rand}";
    }
}
