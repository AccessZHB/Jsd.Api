namespace Jsd.Api.Models.Price;

/// <summary>
/// 充值管理 / 余额流水模块权限标识（对应 sys_menu.permission）。
/// 与 [Permission] 特性配合使用，超管由 PermissionFilter 的 IsSuper 放行。
/// 权限码与数据库 sys_menu 保持一致（member:recharge:* / member:balance-log:*）。
/// </summary>
public static class RechargePermissions
{
    // ---------- 充值管理（菜单 702）----------

    /// <summary>充值管理 - 列表</summary>
    public const string RechargeList = "member:recharge:list";

    /// <summary>充值管理 - 详情</summary>
    public const string RechargeDetail = "member:recharge:detail";

    /// <summary>充值管理 - 创建充值单</summary>
    public const string RechargeCreate = "member:recharge:create";

    /// <summary>充值管理 - 后台代客充值（线下收款直接入账）</summary>
    public const string RechargeAdminCreate = "member:recharge:admin-create";

    /// <summary>充值管理 - 充值退款</summary>
    public const string RechargeRefund = "member:recharge:refund";

    // ---------- 余额流水（菜单 703）----------

    /// <summary>余额流水 - 查询/导出（全平台资金流水，仅限财务）</summary>
    public const string BalanceLog = "member:balance-log:list";
}
