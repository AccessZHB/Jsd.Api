namespace Jsd.Api.Models.StockQuery;

/// <summary>
/// 库存查询权限标识常量（与 sys_menu.permission 一一对应）。
/// 本项目后端只挂 [Authorize] 做登录校验，权限由前端 v-permission 指令消费这些标识。
///
/// 本模块是纯只读查询模块（无新增/编辑/删除），3 个权限分别对应 3 个页面：
/// 总览 / 明细 / 日志。
/// </summary>
public static class StockQueryPermissions
{
    /// <summary>库存总览（统计看板）</summary>
    public const string Overview = "stock_query:overview";

    /// <summary>库存明细（SKU 库存列表）</summary>
    public const string Detail = "stock_query:detail";

    /// <summary>变动日志</summary>
    public const string Log = "stock_query:log";
}
