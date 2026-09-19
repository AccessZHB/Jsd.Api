using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 收款记录 仓储实现（含 trx_payment_log 支付日志的读写）
/// </summary>
public class PaymentRepository : Repository<MktPayment>, IPaymentRepository
{
    /// <summary>收款单号前缀（与订单号 ORD、会员号 M 保持一致的命名风格）</summary>
    private const string NoPrefix = "PAY";

    public PaymentRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<(List<PaymentRow> Items, int Total)> GetPagedListAsync(
        long? memberId, string? memberName, string? paymentNo, int? status,
        DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        var query = Db.MktPayments.AsNoTracking().AsQueryable();

        // 客户ID（精确匹配）——发起核销跳转场景用，避免同名客户串单
        if (memberId.HasValue)
        {
            query = query.Where(p => p.MemberId == memberId.Value);
        }

        // 客户名称（模糊）
        if (!string.IsNullOrWhiteSpace(memberName))
        {
            var kw = memberName.Trim();
            query = query.Where(p => Db.MemMembers.Any(m => m.Id == p.MemberId && m.Name.Contains(kw)));
        }

        // 收款单号（模糊）
        if (!string.IsNullOrWhiteSpace(paymentNo))
        {
            var kw = paymentNo.Trim();
            query = query.Where(p => p.PaymentNo.Contains(kw));
        }

        // 状态：0-待核销 1-已核销
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        // 打款日期范围（endTime 含当天）
        if (startTime.HasValue)
        {
            query = query.Where(p => p.PaymentDate >= startTime.Value.Date);
        }
        if (endTime.HasValue)
        {
            query = query.Where(p => p.PaymentDate < endTime.Value.Date.AddDays(1));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 批量补全客户名称
        var memberIds = items.Select(p => p.MemberId).Distinct().ToList();
        var memberNameMap = await Db.MemMembers.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var rows = items.Select(p => new PaymentRow
        {
            Payment = p,
            MemberName = memberNameMap.TryGetValue(p.MemberId, out var name) ? name : string.Empty
        }).ToList();

        return (rows, total);
    }

    public async Task<MktPayment?> GetByIdTrackedAsync(long id)
    {
        return await Db.MktPayments.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<MktPayment?> GetByPaymentNoAsync(string paymentNo)
    {
        return await Db.MktPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentNo == paymentNo);
    }

    public async Task<List<PaymentItemRow>> GetWriteOffItemsAsync(long paymentId)
    {
        // mkt_payment_item → trx_receivable → trx_order（取订单号）
        var query = from pi in Db.MktPaymentItems.AsNoTracking()
                    join r in Db.TrxReceivables.AsNoTracking() on pi.ReceivableId equals r.Id
                    join o in Db.TrxOrders.AsNoTracking() on r.OrderId equals o.Id
                    where pi.PaymentId == paymentId
                    orderby pi.Id
                    select new PaymentItemRow
                    {
                        Item = pi,
                        OrderNo = o.OrderNo
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// 生成唯一收款单号：PAY + yyyyMMddHHmmss + 4位随机数。
    /// 与订单号同理：uk_payment_no 唯一索引是最后一道防线，业务侧先查重再重试（最多 5 次）。
    /// </summary>
    public async Task<string> GeneratePaymentNoAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var no = $"{NoPrefix}{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
            if (await Db.MktPayments.AsNoTracking().AllAsync(p => p.PaymentNo != no))
            {
                return no;
            }
        }
        // 极端并发兜底：时间戳 + GUID 前 6 位
        return $"{NoPrefix}{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid().ToString("N")[..6]}";
    }

    public async Task AddItemsAsync(List<MktPaymentItem> items)
    {
        await Db.MktPaymentItems.AddRangeAsync(items);
    }

    // ==================== 支付日志 trx_payment_log ====================

    public async Task<List<TrxPaymentLog>> GetLogsByOrderIdAsync(long orderId)
    {
        // 排序：按【回调时间】倒序，最新的一条在最上面（排查掉单时最关心最近一次回调）。
        // 极少数日志可能没有回调时间（如本地预创建的待支付记录），用创建时间兜底。
        return await Db.TrxPaymentLogs.AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .OrderByDescending(l => l.CallbackTime ?? l.CreateTime)
            .ThenByDescending(l => l.Id)
            .ToListAsync();
    }

    public async Task<TrxPaymentLog?> GetLogByTransactionIdAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) return null;

        return await Db.TrxPaymentLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TransactionId == transactionId);
    }

    public async Task<TrxPaymentLog?> GetLogByOutTradeNoAsync(string outTradeNo)
    {
        return await Db.TrxPaymentLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.OutTradeNo == outTradeNo);
    }

    public async Task<TrxPaymentLog?> GetLogByTransactionIdTrackedAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) return null;

        return await Db.TrxPaymentLogs
            .FirstOrDefaultAsync(l => l.TransactionId == transactionId);
    }

    public async Task<TrxPaymentLog?> GetLogByOutTradeNoTrackedAsync(string outTradeNo)
    {
        return await Db.TrxPaymentLogs
            .FirstOrDefaultAsync(l => l.OutTradeNo == outTradeNo);
    }

    public async Task AddLogAsync(TrxPaymentLog log)
    {
        await Db.TrxPaymentLogs.AddAsync(log);
    }
}
