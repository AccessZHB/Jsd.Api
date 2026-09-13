namespace Jsd.Api.Models.StockCheck;

/// <summary>
/// 盘点管理模块 —— 按钮级权限标识（菜单权限 Permission 字段取值）。
/// 与 SysMenu.Permission 对应；现有项目统一用 [Authorize] 做登录鉴权，
/// 本类仅作权限码集中管理与接口文档标注，不引入自定义授权过滤器（避免破坏现有鉴权链路）。
///
/// 5 个按钮级权限标识对照表：
/// ┌──────────────────────┬──────────────────────────┬────────────────────────────┐
/// │ 权限标识 (Permission) │ 对应接口                 │ 说明                       │
/// ├──────────────────────┼──────────────────────────┼────────────────────────────┤
/// │ stock_check:list     │ GET    /api/stockCheck         │ 盘点单分页列表查询      │
/// │ stock_check:detail   │ GET    /api/stockCheck/{id}    │ 盘点单详情（含明细）    │
/// │ stock_check:add      │ POST   /api/stockCheck         │ 创建盘点单              │
/// │ stock_check:edit     │ PUT    /api/stockCheck/{id}    │ 完成盘点（录入实盘）    │
/// │ stock_check:delete   │ DELETE /api/stockCheck/{id}    │ 删除盘点单（仅盘点中）  │
/// └──────────────────────┴──────────────────────────┴────────────────────────────┘
///
/// 注：Jsd_order.sql 菜单种子数据中曾用 stock_check:audit，本模块以"无审核、删除代替审核"为准，
/// 应使用上方 delete 标识（脚本已同步修正）。
/// </summary>
public static class StockCheckPermissions
{
    /// <summary>盘点单列表查询</summary>
    public const string List = "stock_check:list";

    /// <summary>盘点单详情</summary>
    public const string Detail = "stock_check:detail";

    /// <summary>新增盘点单</summary>
    public const string Add = "stock_check:add";

    /// <summary>完成盘点（编辑）</summary>
    public const string Edit = "stock_check:edit";

    /// <summary>删除盘点单</summary>
    public const string Delete = "stock_check:delete";
}
