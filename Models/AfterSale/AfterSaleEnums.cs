namespace Jsd.Api.Models.AfterSale;

/// <summary>
/// 售后模块 状态枚举与展示映射（单一数据源，Service/Controller/前端文案共用）
/// </summary>

/// <summary>
/// 退款单状态（trx_refund.status）
/// </summary>
public enum RefundStatus
{
    /// <summary>0-待审核（客服处理中）</summary>
    Pending = 0,
    /// <summary>1-审核通过/待退款（等待财务打款；退货退款还需等仓库收货）</summary>
    Approved = 1,
    /// <summary>2-已退款（终态，已打款）</summary>
    Refunded = 2,
    /// <summary>3-已驳回（终态）</summary>
    Rejected = 3
}

/// <summary>
/// 退款类型（trx_refund.refund_type）
/// </summary>
public enum RefundType
{
    /// <summary>1-仅退款（不涉及实物退回）</summary>
    RefundOnly = 1,
    /// <summary>2-退货退款（需客户寄回实物，仓库收货后才退款）</summary>
    ReturnAndRefund = 2
}

/// <summary>
/// 退货单状态（trx_return.status）
/// </summary>
public enum ReturnStatus
{
    /// <summary>0-待审核</summary>
    Pending = 0,
    /// <summary>1-已审核/待退货（等待客户寄回）</summary>
    Approved = 1,
    /// <summary>2-已发货/已寄回（客户已寄出）</summary>
    Shipped = 2,
    /// <summary>3-仓库已收货</summary>
    Received = 3,
    /// <summary>4-已退款（终态）</summary>
    Refunded = 4,
    /// <summary>5-已拒绝（终态）</summary>
    Rejected = 5
}

/// <summary>
/// 退货明细状态（trx_return_item.status）
/// </summary>
public enum ReturnItemStatus
{
    /// <summary>0-待确认</summary>
    Pending = 0,
    /// <summary>1-已收货</summary>
    Received = 1,
    /// <summary>2-已拒收</summary>
    Rejected = 2
}

/// <summary>
/// 售后日志动作（trx_refund_log.action）
/// </summary>
public static class RefundLogActions
{
    public const string Created = "created";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string ReturnCreated = "return_created";
    public const string ReturnReceived = "return_received";
    public const string Refunded = "refunded";
}

/// <summary>退款状态 → 中文名</summary>
public static class RefundStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)RefundStatus.Pending => "待审核",
        (int)RefundStatus.Approved => "待退款",
        (int)RefundStatus.Refunded => "已退款",
        (int)RefundStatus.Rejected => "已驳回",
        _ => "未知"
    };
}

/// <summary>退款类型 → 中文名</summary>
public static class RefundTypeHelper
{
    public static string GetName(int type) => type switch
    {
        (int)RefundType.RefundOnly => "仅退款",
        (int)RefundType.ReturnAndRefund => "退货退款",
        _ => "未知"
    };
}

/// <summary>退货单状态 → 中文名</summary>
public static class ReturnStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)ReturnStatus.Pending => "待审核",
        (int)ReturnStatus.Approved => "待退货",
        (int)ReturnStatus.Shipped => "已寄回",
        (int)ReturnStatus.Received => "已收货",
        (int)ReturnStatus.Refunded => "已退款",
        (int)ReturnStatus.Rejected => "已拒绝",
        _ => "未知"
    };
}

/// <summary>退货明细状态 → 中文名</summary>
public static class ReturnItemStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)ReturnItemStatus.Pending => "待确认",
        (int)ReturnItemStatus.Received => "已收货",
        (int)ReturnItemStatus.Rejected => "已拒收",
        _ => "未知"
    };
}

/// <summary>售后日志动作 → 中文名</summary>
public static class RefundLogActionHelper
{
    public static string GetName(string? action) => action switch
    {
        RefundLogActions.Created => "提交申请",
        RefundLogActions.Approved => "审核通过",
        RefundLogActions.Rejected => "审核驳回",
        RefundLogActions.ReturnCreated => "生成退货单",
        RefundLogActions.ReturnReceived => "仓库收货",
        RefundLogActions.Refunded => "执行退款",
        _ => "操作"
    };
}

/// <summary>
/// 售后模块权限标识（供前端菜单/按钮配置）
/// </summary>
public static class AfterSalePermissions
{
    public const string RefundList = "aftersale:refund:list";
    public const string RefundCreate = "aftersale:refund:create";
    public const string RefundApprove = "aftersale:refund:approve";
    public const string RefundExecute = "aftersale:refund:execute";
    public const string ReturnList = "aftersale:return:list";
    public const string ReturnCreate = "aftersale:return:create";
    public const string ReturnReceive = "aftersale:return:receive";
    public const string LogList = "aftersale:log:list";
}
