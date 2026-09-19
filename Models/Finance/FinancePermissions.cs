namespace Jsd.Api.Models.Finance;

/// <summary>
/// 财务结算模块 —— 菜单权限标识常量
///
/// 与 sys_menu 中的菜单种子保持一致（见 Jsd_order.sql 菜单 320/321/322）：
///   320 财务结算（目录）
///   321 应收账款 /finance/receivable  → finance:receivable:list
///   322 收款记录 /finance/payment     → finance:payment:list / add / verify
///
/// 说明：trx_payment_log（支付日志）为内部日志表，不设独立菜单，
/// 因此没有对应的菜单权限；其读写接口由 PaymentCallbackController 内部调用。
/// </summary>
public static class FinancePermissions
{
    /// <summary>应收账款 - 查看列表（菜单 321）</summary>
    public const string ReceivableList = "finance:receivable:list";

    /// <summary>收款记录 - 查看列表（菜单 322）</summary>
    public const string PaymentList = "finance:payment:list";

    /// <summary>收款记录 - 录入收款（按钮 323）</summary>
    public const string PaymentAdd = "finance:payment:add";

    /// <summary>收款记录 - 核销账单（按钮 324）</summary>
    public const string PaymentVerify = "finance:payment:verify";
}
