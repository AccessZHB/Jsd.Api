namespace Jsd.Api.Models.Payment;

/// <summary>
/// 微信支付 T+1 对账模块 —— 菜单权限标识常量
///
/// 说明：本模块当前与支付模块同源配置，但 ReconciliationController 仅用 [Authorize] 做登录鉴权，
/// 未做逐接口的策略授权。这里的常量作为【权限三处一致】的权威来源：
///   后端常量 = 前端 v-permission = sys_menu.permission。
/// 前端 invoice_reconciliation_menu.sql 已按下列标识建菜单种子。
/// </summary>
public static class ReconciliationPermissions
{
    /// <summary>对账管理 - 查看列表 / 报告</summary>
    public const string ReconciliationList = "finance:reconciliation:list";

    /// <summary>对账管理 - 手动下载账单</summary>
    public const string ReconciliationDownload = "finance:reconciliation:download";

    /// <summary>对账管理 - 手动触发对账</summary>
    public const string ReconciliationRun = "finance:reconciliation:run";

    /// <summary>对账管理 - 差异核销处理</summary>
    public const string ReconciliationHandle = "finance:reconciliation:handle";
}
