namespace Jsd.Api.Models.StockWarning;

/// <summary>
/// 库存预警权限标识常量（与 sys_menu.permission 一一对应）。
/// 本项目后端只挂 [Authorize] 做登录校验，按钮级权限由前端 v-permission 指令消费这些标识。
///
/// 注意：本模块只有 3 个权限（列表 / 编辑 / 启用停用），没有独立的删除权限；
/// 删除操作作为"配置编辑"的一部分，复用 stock_warning:edit 控制。
/// </summary>
public static class StockWarningPermissions
{
    /// <summary>查看预警配置列表</summary>
    public const string List = "stock_warning:list";

    /// <summary>新增 / 编辑 / 删除预警配置（编辑域，删除复用此权限）</summary>
    public const string Edit = "stock_warning:edit";

    /// <summary>启用 / 停用切换</summary>
    public const string Enable = "stock_warning:enable";
}
