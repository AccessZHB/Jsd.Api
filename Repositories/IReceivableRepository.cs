using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 应收账款 仓储接口（继承泛型仓储，复用 GetByIdAsync / AddAsync / SaveChangesAsync 等通用能力）
/// </summary>
public interface IReceivableRepository : IRepository<TrxReceivable>
{
    /// <summary>
    /// 分页查询应收账款列表（关联补全订单号与客户名称）。
    /// 筛选：客户ID（精确） / 客户名称（模糊） / 订单号（模糊） / 结算状态 / 到期日范围。
    /// </summary>
    Task<(List<ReceivableRow> Items, int Total)> GetPagedListAsync(
        long? memberId, string? memberName, string? orderNo, int? status,
        DateTime? dueStart, DateTime? dueEnd, int page, int pageSize);

    /// <summary>带跟踪查询（核销时需要更新 paid_amount / status）</summary>
    Task<TrxReceivable?> GetByIdTrackedAsync(long id);

    /// <summary>根据订单ID查询应收记录（同步时用于幂等判断，避免重复生成）</summary>
    Task<TrxReceivable?> GetByOrderIdAsync(long orderId);

    /// <summary>
    /// 查询某笔应收的核销记录（Join mkt_payment_item + mkt_payment）
    /// </summary>
    Task<List<WriteOffRow>> GetWriteOffsAsync(long receivableId);
}

/// <summary>
/// 应收账款列表行（实体 + 联查补全的展示字段）
/// 用具体类型承载投影结果，避免 dynamic 带来的运行时风险。
/// </summary>
public class ReceivableRow
{
    /// <summary>应收实体</summary>
    public TrxReceivable Receivable { get; set; } = null!;

    /// <summary>订单号（trx_order.order_no）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户名称（mem_member.name）</summary>
    public string MemberName { get; set; } = string.Empty;
}

/// <summary>
/// 核销记录行（mkt_payment_item + 所属收款单的展示字段）
/// </summary>
public class WriteOffRow
{
    /// <summary>核销明细实体</summary>
    public MktPaymentItem Item { get; set; } = null!;

    /// <summary>收款单号</summary>
    public string PaymentNo { get; set; } = string.Empty;

    /// <summary>收款方式</summary>
    public int PaymentMethod { get; set; }

    /// <summary>打款/到账日期</summary>
    public DateTime PaymentDate { get; set; }
}
