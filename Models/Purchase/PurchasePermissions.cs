namespace Jsd.Api.Models.Purchase;

/// <summary>
/// 采购管理模块 —— 权限标识（与前端 v-permission 对应）。
/// 后端 Controller 当前统一用 [Authorize] 鉴权（与订单/库存等模块一致），
/// 以下常量供前端菜单/按钮权限配置与未来精细化策略使用。
/// </summary>
public static class PurchasePermissions
{
    // 采购订单
    public const string OrderList = "purchase:order:list";
    public const string OrderCreate = "purchase:order:create";
    public const string OrderUpdate = "purchase:order:update";
    /// <summary>确认订单（草稿 → 待入库）。本模块无审核环节，故不设 approve 权限</summary>
    public const string OrderConfirm = "purchase:order:confirm";
    public const string OrderCancel = "purchase:order:cancel";
    public const string OrderAutoCreate = "purchase:order:auto-create";

    // 采购入库
    public const string InboundList = "purchase:inbound:list";
    public const string InboundCreate = "purchase:inbound:create";
    public const string InboundConfirm = "purchase:inbound:confirm";

    // 应付账款
    public const string PayableList = "purchase:payable:list";
    public const string PayablePay = "purchase:payable:pay";
}
