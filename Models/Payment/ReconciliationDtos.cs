namespace Jsd.Api.Models.Payment;

/// <summary>微信账单一行明细的中间结构（CSV 解析产物，单位统一为「元」）</summary>
public class BillRowDto
{
    /// <summary>微信交易号</summary>
    public string? TransactionId { get; set; }

    /// <summary>商户订单号</summary>
    public string? OutTradeNo { get; set; }

    /// <summary>交易时间</summary>
    public DateTime? TradeTime { get; set; }

    /// <summary>交易状态（SUCCESS / REFUND / CLOSED…）</summary>
    public string? TradeState { get; set; }

    /// <summary>订单金额（元）</summary>
    public decimal TotalFee { get; set; }

    /// <summary>退款金额（元）</summary>
    public decimal RefundFee { get; set; }

    /// <summary>手续费（元）</summary>
    public decimal Poundage { get; set; }
}

/// <summary>账单下载 / 解析结果</summary>
public class BillDownloadDto
{
    /// <summary>主表ID</summary>
    public long BillId { get; set; }

    /// <summary>账单日期</summary>
    public string BillDate { get; set; } = string.Empty;

    /// <summary>账单类型（ALL/SUCCESS/REFUND）</summary>
    public string BillType { get; set; } = "ALL";

    /// <summary>总交易笔数</summary>
    public int TotalCount { get; set; }

    /// <summary>总金额（元）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>退款金额合计（元）</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>手续费合计（元）</summary>
    public decimal TotalFee { get; set; }

    /// <summary>对账状态（见 BillReconStatus）</summary>
    public int ReconStatus { get; set; }

    /// <summary>是否跳过（当日账单已存在且未强制刷新）</summary>
    public bool Skipped { get; set; }

    /// <summary>说明</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>对账差异项（报告里的明细行）</summary>
public class ReconDiffItemDto
{
    /// <summary>差异记录ID</summary>
    public long Id { get; set; }

    /// <summary>商户订单号</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信交易号</summary>
    public string? TransactionId { get; set; }

    /// <summary>差异类型</summary>
    public int DiffType { get; set; }

    /// <summary>差异类型名称</summary>
    public string DiffTypeName { get; set; } = string.Empty;

    /// <summary>微信金额（元）</summary>
    public decimal WechatAmount { get; set; }

    /// <summary>系统金额（元）</summary>
    public decimal SystemAmount { get; set; }

    /// <summary>差额（微信 - 系统，元）</summary>
    public decimal DiffAmount { get; set; }

    /// <summary>微信侧状态</summary>
    public string? WechatStatus { get; set; }

    /// <summary>系统侧状态</summary>
    public int? SystemStatus { get; set; }
}

/// <summary>差异核查列表项（含人工处理信息，供后台核查页使用）</summary>
public class ReconCheckDto
{
    /// <summary>差异记录ID</summary>
    public long Id { get; set; }

    /// <summary>账单日期</summary>
    public string BillDate { get; set; } = string.Empty;

    /// <summary>关联订单ID（payment_order.id，长款时为 0）</summary>
    public long OrderId { get; set; }

    /// <summary>商户订单号</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信交易号</summary>
    public string? TransactionId { get; set; }

    /// <summary>差异类型</summary>
    public int DiffType { get; set; }

    /// <summary>差异类型名称</summary>
    public string DiffTypeName { get; set; } = string.Empty;

    /// <summary>微信金额（元）</summary>
    public decimal WechatAmount { get; set; }

    /// <summary>系统金额（元）</summary>
    public decimal SystemAmount { get; set; }

    /// <summary>差额（微信 - 系统，元）</summary>
    public decimal DiffAmount { get; set; }

    /// <summary>微信侧状态</summary>
    public string? WechatStatus { get; set; }

    /// <summary>系统侧状态</summary>
    public int? SystemStatus { get; set; }

    /// <summary>系统侧状态名称</summary>
    public string SystemStatusName { get; set; } = string.Empty;

    /// <summary>处理状态（见 CheckHandleStatus）</summary>
    public int HandleStatus { get; set; }

    /// <summary>处理状态名称</summary>
    public string HandleStatusName { get; set; } = string.Empty;

    /// <summary>处理人</summary>
    public string? Handler { get; set; }

    /// <summary>处理备注</summary>
    public string? Remark { get; set; }

    /// <summary>核查时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 今日（指定账单日）对账报告。
/// ⚠️ 这是 AutoReconciliation 的最终产出，也是定时任务 JobResultBuffer / 接口返回的载荷。
/// </summary>
public class ReconciliationReportDto
{
    /// <summary>账单日期</summary>
    public string BillDate { get; set; } = string.Empty;

    /// <summary>账单主表ID</summary>
    public long BillId { get; set; }

    // ---------- 微信侧 ----------
    /// <summary>微信账单交易总笔数</summary>
    public int WechatCount { get; set; }

    /// <summary>微信账单支付成功笔数</summary>
    public int WechatSuccessCount { get; set; }

    /// <summary>微信账单总金额（元）</summary>
    public decimal WechatAmount { get; set; }

    /// <summary>微信账单支付成功金额（元）</summary>
    public decimal WechatSuccessAmount { get; set; }

    /// <summary>微信退款金额合计（元）</summary>
    public decimal WechatRefundAmount { get; set; }

    /// <summary>微信手续费合计（元）</summary>
    public decimal WechatFee { get; set; }

    // ---------- 系统侧 ----------
    /// <summary>系统本地支付成功笔数</summary>
    public int SystemCount { get; set; }

    /// <summary>系统本地支付成功金额（元）</summary>
    public decimal SystemAmount { get; set; }

    // ---------- 差异 ----------
    /// <summary>异常总笔数</summary>
    public int DiffCount { get; set; }

    /// <summary>长款笔数（仅微信有）</summary>
    public int WechatOnlyCount { get; set; }

    /// <summary>短款笔数（仅系统有）</summary>
    public int SystemOnlyCount { get; set; }

    /// <summary>金额不符笔数</summary>
    public int AmountDiffCount { get; set; }

    /// <summary>状态不符笔数</summary>
    public int StatusDiffCount { get; set; }

    /// <summary>差异金额合计（元，取绝对值之和，供财务快速判断风险敞口）</summary>
    public decimal DiffAmount { get; set; }

    // ---------- 结论 ----------
    /// <summary>是否账平（无差异）</summary>
    public bool Balanced { get; set; }

    /// <summary>结论摘要一句话（落 wechat_daily_bill.recon_result）</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>差异明细（最多返回前 200 条，全量请查差异列表接口）</summary>
    public List<ReconDiffItemDto> Diffs { get; set; } = new();
}
