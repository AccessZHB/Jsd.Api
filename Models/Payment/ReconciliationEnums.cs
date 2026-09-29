namespace Jsd.Api.Models.Payment;

/// <summary>微信账单类型（对应 wechat_daily_bill.bill_type，同时也是微信 request 的 bill_type 参数）</summary>
public enum BillType : int
{
    /// <summary>ALL - 所有账单（含成功支付、退款、撤销等全部明细）</summary>
    All = 1,

    /// <summary>SUCCESS - 仅支付成功账单</summary>
    Success = 2,

    /// <summary>REFUND - 仅退款账单</summary>
    Refund = 3
}

/// <summary>账单类型 → 微信 API 兼容的字符串（ALL / SUCCESS / REFUND）</summary>
public static class BillTypeNames
{
    public static string ToWeChatString(int billType) => billType switch
    {
        (int)BillType.All => "ALL",
        (int)BillType.Success => "SUCCESS",
        (int)BillType.Refund => "REFUND",
        _ => "ALL"
    };

    public static int FromWeChatString(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "SUCCESS" => (int)BillType.Success,
        "REFUND" => (int)BillType.Refund,
        _ => (int)BillType.All
    };
}

/// <summary>微信对账单处理状态（对应 wechat_daily_bill.recon_status）</summary>
public enum BillReconStatus : int
{
    /// <summary>待下载（记录已建，或下载失败待重试）</summary>
    Pending = 0,

    /// <summary>处理中（已申请到账单正在落明细 / 正在比对）</summary>
    Processing = 1,

    /// <summary>对账完成（无论是否有差异，只要流程走完即此态）</summary>
    Finished = 2,

    /// <summary>异常（下载/解析/比对任一环节失败）</summary>
    Error = 3
}

/// <summary>对账状态 → 中文名</summary>
public static class BillReconStatusNames
{
    public static string GetName(int status) => status switch
    {
        (int)BillReconStatus.Pending => "待下载",
        (int)BillReconStatus.Processing => "处理中",
        (int)BillReconStatus.Finished => "对账完成",
        (int)BillReconStatus.Error => "异常",
        _ => "未知"
    };
}

/// <summary>对账差异类型（对应 reconciliation_check.diff_type）</summary>
public enum DiffType : int
{
    /// <summary>长款：仅微信有（用户在微信付了钱，但系统没有成功记录 —— 典型的掉单，需优先排查补单）</summary>
    WechatOnly = 1,

    /// <summary>短款：仅系统有（系统标记支付成功，但微信账单没有 —— 可能是假成功/长款，需核查）</summary>
    SystemOnly = 2,

    /// <summary>金额不符：两边都有，但金额对不上</summary>
    AmountDiff = 3,

    /// <summary>状态不符：两边都有且金额一致，但状态不一致（如微信已 REFUND，系统仍 SUCCESS）</summary>
    StatusDiff = 4
}

/// <summary>差异类型 → 中文名</summary>
public static class DiffTypeNames
{
    public static string GetName(int diffType) => diffType switch
    {
        (int)DiffType.WechatOnly => "长款-仅微信有",
        (int)DiffType.SystemOnly => "短款-仅系统有",
        (int)DiffType.AmountDiff => "金额不符",
        (int)DiffType.StatusDiff => "状态不符",
        _ => "未知"
    };
}

/// <summary>差异处理状态（对应 reconciliation_check.handle_status）</summary>
public enum CheckHandleStatus : int
{
    /// <summary>待处理</summary>
    Pending = 0,

    /// <summary>已处理（核销完毕）</summary>
    Handled = 1,

    /// <summary>挂账（本周期不清，挂账待后续处理）</summary>
    Suspended = 2
}

/// <summary>差异处理状态 → 中文名</summary>
public static class CheckHandleStatusNames
{
    public static string GetName(int status) => status switch
    {
        (int)CheckHandleStatus.Pending => "待处理",
        (int)CheckHandleStatus.Handled => "已处理",
        (int)CheckHandleStatus.Suspended => "挂账",
        _ => "未知"
    };
}

/// <summary>
/// 金额比较容差（单位：元）。
/// 微信账单是「元（2 位小数）」，系统侧 payment_order.amount 是「分」换算来的元，
/// 0.005 即半分容差：只要差值小于半分就认为是同一金额，避免精度舍入导致的噪音差异。
/// </summary>
public static class ReconAmountTolerance
{
    public const decimal Yuan = 0.005m;
}
