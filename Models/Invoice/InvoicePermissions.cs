namespace Jsd.Api.Models.Invoice;

/// <summary>
/// 发票与税务管理模块 —— 菜单权限标识常量
///
/// 说明：本模块当前为【纯后端】交付，尚未在 sys_menu 建菜单（避免前端无对应页面导致白屏）。
/// 待前端页面（views/finance/invoice/index.vue 等）就绪后，按下列标识补菜单种子即可，
/// 权限三处一致：后端常量 = 前端 v-permission = sys_menu.permission。
/// </summary>
public static class InvoicePermissions
{
    /// <summary>开票记录 - 查看列表</summary>
    public const string InvoiceList = "finance:invoice:list";

    /// <summary>开票记录 - 提交开票申请</summary>
    public const string InvoiceApply = "finance:invoice:apply";

    /// <summary>开票记录 - 财务回写票面（开票）</summary>
    public const string InvoiceIssue = "finance:invoice:issue";

    /// <summary>开票记录 - 作废/红冲</summary>
    public const string InvoiceCancel = "finance:invoice:cancel";

    /// <summary>客户开票信息 - 查看</summary>
    public const string CustomerInvoiceList = "finance:invoice:info:list";

    /// <summary>客户开票信息 - 新增/修改</summary>
    public const string CustomerInvoiceSave = "finance:invoice:info:save";
}
