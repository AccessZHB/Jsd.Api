namespace Jsd.Api.Models.Finance;

/// <summary>
/// 应收账款状态常量（严格对应 trx_receivable.status 建表注释：0-未结清 1-已结清 2-已逾期）
///
/// 【重要】建表只定义了 3 个状态码，**"部分结清"不是独立的状态码**，
/// 而是由 paid_amount &gt; 0 且 unpaid_amount &gt; 0 推导出的展示态（见 GetSettleName）。
/// 这样既严格遵循数据库字段定义，又能满足前端"未结清/部分结清/已结清"的展示需求。
/// </summary>
public static class ReceivableStatus
{
    /// <summary>0-未结清（含"部分结清"：paid_amount &gt; 0 但 unpaid_amount &gt; 0）</summary>
    public const int Unsettled = 0;

    /// <summary>1-已结清（unpaid_amount = 0）</summary>
    public const int Settled = 1;

    /// <summary>2-已逾期（超过到期日仍未结清）</summary>
    public const int Overdue = 2;

    /// <summary>状态码 → 中文名（数据库原生状态）</summary>
    public static string GetName(int status) => status switch
    {
        Unsettled => "未结清",
        Settled => "已结清",
        Overdue => "已逾期",
        _ => "未知"
    };

    /// <summary>
    /// 结算展示态（含推导出的"部分结清"）：
    ///   已结清 → 部分结清 → 已逾期 → 未结清（优先级从高到低）
    /// </summary>
    public static string GetSettleName(decimal paidAmount, decimal unpaidAmount, int status)
    {
        if (unpaidAmount <= 0) return "已结清";
        if (paidAmount > 0) return "部分结清";
        return status == Overdue ? "已逾期" : "未结清";
    }
}

/// <summary>
/// 应收单号格式化工具。
/// trx_receivable 建表没有 receivable_no 字段，为避免改表，统一由主键派生展示单号。
/// 格式：AR + ID 补零到 8 位（如 AR00000001）。纯展示用途，不落库。
/// </summary>
public static class ReceivableNoFormatter
{
    /// <summary>前缀</summary>
    public const string Prefix = "AR";

    /// <summary>按主键生成应收单号</summary>
    public static string Format(long id) => $"{Prefix}{id:D8}";
}

/// <summary>
/// 应收账款分页查询条件
/// </summary>
public class ReceivablePageQuery
{
    /// <summary>客户名称（模糊匹配 mem_member.name）</summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// 客户ID（精确匹配 trx_receivable.member_id）。
    /// 核销弹窗"选择该客户名下的应收单"时用精确ID，避免按名称模糊匹配串到同名客户。
    /// </summary>
    public long? MemberId { get; set; }

    /// <summary>订单号（模糊匹配 trx_order.order_no）</summary>
    public string? OrderNo { get; set; }

    /// <summary>结算状态：0-未结清 1-已结清 2-已逾期（null 表示全部）</summary>
    public int? Status { get; set; }

    /// <summary>到期日起（含，按 trx_receivable.due_date 过滤）</summary>
    public DateTime? DueStart { get; set; }

    /// <summary>到期日止（含，按 trx_receivable.due_date 过滤）</summary>
    public DateTime? DueEnd { get; set; }

    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// 应收账款列表项 DTO
/// </summary>
public class ReceivableListDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>
    /// 应收单号（展示用派生编号：AR + ID 补零到 8 位，如 AR00000001）。
    /// 【说明】trx_receivable 建表没有 receivable_no 字段，为避免改表，
    /// 这里由后端按主键派生一个稳定、可读的单号，纯展示用途，不落库。
    /// </summary>
    public string ReceivableNo { get; set; } = string.Empty;

    /// <summary>关联订单ID</summary>
    public long OrderId { get; set; }

    /// <summary>订单号（冗余自 trx_order.order_no，便于列表直接展示）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户ID</summary>
    public long MemberId { get; set; }

    /// <summary>客户名称（Join mem_member.name）</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>应收总额</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>已收金额</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>未收金额</summary>
    public decimal UnpaidAmount { get; set; }

    /// <summary>订单日期</summary>
    public DateTime OrderDate { get; set; }

    /// <summary>到期日</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>状态：0-未结清 1-已结清 2-已逾期</summary>
    public int Status { get; set; }

    /// <summary>状态中文名</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>结算展示态（含推导的"部分结清"）</summary>
    public string SettleName { get; set; } = string.Empty;

    /// <summary>是否已逾期（到期日早于今天且未结清；供前端标红）</summary>
    public bool IsOverdue { get; set; }

    /// <summary>账龄天数（距订单日期的自然日；用于账龄分析）</summary>
    public int AgingDays { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 应收账款详情 DTO（列表字段 + 该笔应收的核销记录）
/// </summary>
public class ReceivableDetailDto : ReceivableListDto
{
    /// <summary>关联的核销记录（Join mkt_payment_item + mkt_payment）</summary>
    public List<WriteOffRecordDto> WriteOffs { get; set; } = new();
}

/// <summary>
/// 单条核销记录 DTO（一次收款拆分到某张应收单的部分）
/// </summary>
public class WriteOffRecordDto
{
    /// <summary>核销明细ID (mkt_payment_item.id)</summary>
    public long PaymentItemId { get; set; }

    /// <summary>收款单ID</summary>
    public long PaymentId { get; set; }

    /// <summary>收款单号（如 PAY202609150001）</summary>
    public string PaymentNo { get; set; } = string.Empty;

    /// <summary>本次核销金额</summary>
    public decimal OffsetAmount { get; set; }

    /// <summary>收款方式：1-银行转账 2-支票 3-现金 4-其他</summary>
    public int PaymentMethod { get; set; }

    /// <summary>收款方式中文名</summary>
    public string PaymentMethodName { get; set; } = string.Empty;

    /// <summary>打款/到账日期</summary>
    public DateTime PaymentDate { get; set; }

    /// <summary>核销时间</summary>
    public DateTime CreateTime { get; set; }
}
