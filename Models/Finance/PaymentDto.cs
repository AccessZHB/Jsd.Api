namespace Jsd.Api.Models.Finance;

/// <summary>
/// 收款方式常量（对应 mkt_payment.payment_method 建表注释：1-银行转账 2-支票 3-现金 4-其他）
/// </summary>
public static class PaymentMethods
{
    /// <summary>1-银行转账</summary>
    public const int BankTransfer = 1;

    /// <summary>2-支票</summary>
    public const int Check = 2;

    /// <summary>3-现金</summary>
    public const int Cash = 3;

    /// <summary>4-其他</summary>
    public const int Other = 4;

    /// <summary>方式码 → 中文名</summary>
    public static string GetName(int method) => method switch
    {
        BankTransfer => "银行转账",
        Check => "支票",
        Cash => "现金",
        Other => "其他",
        _ => "未知"
    };
}

/// <summary>
/// 收款单状态常量（对应 mkt_payment.status 建表注释：0-待核销 1-已核销）
/// </summary>
public static class PaymentStatus
{
    /// <summary>0-待核销</summary>
    public const int Pending = 0;

    /// <summary>1-已核销</summary>
    public const int WrittenOff = 1;

    /// <summary>状态码 → 中文名</summary>
    public static string GetName(int status) => status switch
    {
        Pending => "待核销",
        WrittenOff => "已核销",
        _ => "未知"
    };
}

/// <summary>
/// 支付日志状态常量（对应 trx_payment_log.status：0-待支付 1-已支付 2-已取消 3-已退款 4-支付失败）
/// </summary>
public static class PayLogStatus
{
    public const int Pending = 0;
    public const int Paid = 1;
    public const int Cancelled = 2;
    public const int Refunded = 3;
    public const int Failed = 4;

    public static string GetName(int status) => status switch
    {
        Pending => "待支付",
        Paid => "已支付",
        Cancelled => "已取消",
        Refunded => "已退款",
        Failed => "支付失败",
        _ => "未知"
    };
}

/// <summary>
/// 支付日志方式常量（对应 trx_payment_log.payment_type：1-微信支付(JSAPI) 2-余额支付 3-其他）
/// </summary>
public static class PayLogTypes
{
    public const int Wechat = 1;
    public const int Balance = 2;
    public const int Other = 3;

    public static string GetName(int type) => type switch
    {
        Wechat => "微信支付",
        Balance => "余额支付",
        Other => "其他",
        _ => "未知"
    };
}

/// <summary>
/// 收款记录列表项 DTO
/// </summary>
public class PaymentListDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>收款单号（唯一）</summary>
    public string PaymentNo { get; set; } = string.Empty;

    /// <summary>客户ID</summary>
    public long MemberId { get; set; }

    /// <summary>客户名称（Join mem_member.name）</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>实际收款金额</summary>
    public decimal Amount { get; set; }

    /// <summary>收款方式：1-银行转账 2-支票 3-现金 4-其他</summary>
    public int PaymentMethod { get; set; }

    /// <summary>收款方式中文名</summary>
    public string PaymentMethodName { get; set; } = string.Empty;

    /// <summary>打款/到账日期</summary>
    public DateTime PaymentDate { get; set; }

    /// <summary>打款凭证/转账截图路径</summary>
    public string? Voucher { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>后台操作人员ID</summary>
    public int OperatorId { get; set; }

    /// <summary>状态：0-待核销 1-已核销</summary>
    public int Status { get; set; }

    /// <summary>状态中文名</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>录入时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 收款记录详情 DTO（列表字段 + 核销明细）
/// </summary>
public class PaymentDetailDto : PaymentListDto
{
    /// <summary>核销明细（该笔收款被拆分到哪些应收单）</summary>
    public List<PaymentItemDto> Items { get; set; } = new();
}

/// <summary>
/// 核销明细项 DTO
/// </summary>
public class PaymentItemDto
{
    /// <summary>核销明细ID</summary>
    public long Id { get; set; }

    /// <summary>应收单ID</summary>
    public long ReceivableId { get; set; }

    /// <summary>对应订单号（便于前端展示）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>核销金额</summary>
    public decimal OffsetAmount { get; set; }

    /// <summary>核销时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 新增收款单入参 DTO
/// </summary>
public class PaymentCreateDto
{
    /// <summary>客户ID（mem_member.id，必填）</summary>
    public long MemberId { get; set; }

    /// <summary>实际收款金额（元，必须 &gt; 0）</summary>
    public decimal Amount { get; set; }

    /// <summary>收款方式：1-银行转账 2-支票 3-现金 4-其他（默认 1）</summary>
    public int PaymentMethod { get; set; } = PaymentMethods.BankTransfer;

    /// <summary>打款/到账日期（必填）</summary>
    public DateTime PaymentDate { get; set; }

    /// <summary>打款凭证/转账截图路径（可空）</summary>
    public string? Voucher { get; set; }

    /// <summary>备注（可空，≤500 字）</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 新增收款单返回结果（供前端提示"收款单 PAYxxx 已创建"）
/// </summary>
public class PaymentCreateResultDto
{
    /// <summary>收款单ID</summary>
    public long Id { get; set; }

    /// <summary>收款单号（后端自动生成）</summary>
    public string PaymentNo { get; set; } = string.Empty;
}

/// <summary>
/// 收款记录分页查询条件
/// </summary>
public class PaymentPageQuery
{
    /// <summary>客户名称（模糊匹配 mem_member.name）</summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// 客户ID（精确匹配 mkt_payment.member_id）。
    /// 从"应收账款 → 发起核销"跳转时需要精确找该客户的待核销收款单，避免同名客户串单。
    /// </summary>
    public long? MemberId { get; set; }

    /// <summary>收款单号（模糊匹配）</summary>
    public string? PaymentNo { get; set; }

    /// <summary>状态：0-待核销 1-已核销（null 表示全部）</summary>
    public int? Status { get; set; }

    /// <summary>打款日期起（含）</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>打款日期止（含）</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// 单条核销明细入参
/// </summary>
public class WriteOffDto
{
    /// <summary>应收账款ID（trx_receivable.id）</summary>
    public long ReceivableId { get; set; }

    /// <summary>本次核销金额（必须 &gt; 0，且不超过该应收单的未收金额）</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// 核销入参 DTO：收款单ID + 核销明细列表
/// </summary>
public class WriteOffInputDto
{
    /// <summary>收款单ID（mkt_payment.id，必须处于"待核销(0)"状态）</summary>
    public long PaymentId { get; set; }

    /// <summary>核销明细列表（各明细金额之和必须恰好等于收款单金额）</summary>
    public List<WriteOffDto> Items { get; set; } = new();
}

/// <summary>
/// 支付日志 DTO（trx_payment_log，供内部对账查询）
/// </summary>
public class PaymentLogDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? OutTradeNo { get; set; }

    /// <summary>支付方式：1-微信支付 2-余额支付 3-其他</summary>
    public int PaymentType { get; set; }

    /// <summary>支付方式中文名</summary>
    public string PaymentTypeName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>支付状态：0-待支付 1-已支付 2-已取消 3-已退款 4-支付失败</summary>
    public int Status { get; set; }

    /// <summary>支付状态中文名</summary>
    public string StatusName { get; set; } = string.Empty;

    public DateTime? CallbackTime { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 微信支付回调处理结果
///
/// 【为什么要单独定义】
/// 重复回调被幂等拦截时，必须能明确告知调用方"该笔已处理过"，
/// 而不是笼统返回成功——否则排查问题时无法区分"本次真正处理"和"重复推送被忽略"。
/// </summary>
public class WechatCallbackResultDto
{
    /// <summary>支付日志ID（trx_payment_log.id）</summary>
    public long LogId { get; set; }

    /// <summary>
    /// 是否为【重复回调】：true 表示同一 transaction_id 此前已成功处理过，
    /// 本次未做任何写入 / 未推进订单 / 未改动金额。
    /// </summary>
    public bool Duplicated { get; set; }

    /// <summary>处理结果描述，如"支付回调处理成功" / "该回调已处理过，已忽略（幂等）"</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 微信支付回调入参 DTO（由 PaymentCallbackController 接收）
/// 实际生产环境微信回调是 XML，这里用 JSON 兼容格式接收，便于内部联调。
/// </summary>
public class WechatCallbackDto
{
    /// <summary>商户订单号（对应 trx_order.order_no）</summary>
    public string OutTradeNo { get; set; } = string.Empty;

    /// <summary>微信支付交易号</summary>
    public string? TransactionId { get; set; }

    /// <summary>支付结果：SUCCESS / FAIL</summary>
    public string ResultCode { get; set; } = string.Empty;

    /// <summary>支付金额（元）</summary>
    public decimal Amount { get; set; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrCode { get; set; }

    /// <summary>错误描述（失败时）</summary>
    public string? ErrMsg { get; set; }

    /// <summary>签名（生产环境需验签）</summary>
    public string? Sign { get; set; }

    /// <summary>回调原始数据（XML/JSON，落库用于对账）</summary>
    public string? RawData { get; set; }
}
