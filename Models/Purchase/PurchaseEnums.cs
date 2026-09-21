namespace Jsd.Api.Models.Purchase;

/// <summary>
/// 采购订单状态枚举（对应 pur_order.status）
/// 0-草稿 1-审批中 2-审批通过/待入库 3-部分入库 4-已结清/已完成 9-作废
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 0,
    Reviewing = 1,
    Approved = 2,
    PartialIn = 3,
    Completed = 4,
    Cancelled = 9
}

/// <summary>
/// 采购入库单状态枚举（对应 pur_inbound.status）
/// 0-待确认 1-已入库
/// </summary>
public enum InboundStatus
{
    Pending = 0,
    Confirmed = 1
}

/// <summary>
/// 应付账款状态枚举（对应 pur_payable.status）
/// 0-未付 1-部分付款 2-已结清
/// </summary>
public enum PayableStatus
{
    Unpaid = 0,
    Partial = 1,
    Settled = 2
}

/// <summary>
/// 采购订单状态中文名 / 展示态辅助方法
/// </summary>
public static class PurchaseOrderStatusHelper
{
    /// <summary>状态 → 中文名</summary>
    public static string GetName(int status) => status switch
    {
        (int)PurchaseOrderStatus.Draft => "草稿",
        (int)PurchaseOrderStatus.Reviewing => "审批中",
        (int)PurchaseOrderStatus.Approved => "审批通过/待入库",
        (int)PurchaseOrderStatus.PartialIn => "部分入库",
        (int)PurchaseOrderStatus.Completed => "已结清",
        (int)PurchaseOrderStatus.Cancelled => "作废",
        _ => "未知"
    };
}

/// <summary>
/// 入库单状态中文名辅助方法
/// </summary>
public static class InboundStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)InboundStatus.Pending => "待确认",
        (int)InboundStatus.Confirmed => "已入库",
        _ => "未知"
    };
}

/// <summary>
/// 应付账款状态中文名辅助方法
/// </summary>
public static class PayableStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)PayableStatus.Unpaid => "未付",
        (int)PayableStatus.Partial => "部分付款",
        (int)PayableStatus.Settled => "已结清",
        _ => "未知"
    };
}
