namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 出库管理模块 —— 按钮级权限标识（菜单权限 Permission 字段取值）。
/// 与 SysMenu.Permission 对应；现有项目统一用 [Authorize] 做登录鉴权，
/// 本类仅作权限码集中管理与接口文档标注，不引入自定义授权过滤器（避免破坏现有鉴权链路）。
///
/// 5 个按钮级权限标识对照表：
/// ┌──────────────────────┬──────────────────────┬────────────────────────────────┐
/// │ 权限标识 (Permission) │ 对应接口             │ 说明                           │
/// ├──────────────────────┼──────────────────────┼────────────────────────────────┤
/// │ stockOut:list        │ GET  /api/stockOut/list    │ 出库单分页列表查询        │
/// │ stockOut:detail      │ GET  /api/stockOut/detail/{id} │ 出库单详情（含明细）  │
/// │ stockOut:create      │ POST /api/stockOut/create    │ 创建出库单              │
/// │ stockOut:update      │ PUT  /api/stockOut/update/{id} │ 编辑出库单            │
/// │ stockOut:delete      │ DELETE /api/stockOut/delete/{id} │ 删除出库单（回补库存）│
/// └──────────────────────┴──────────────────────┴────────────────────────────────┘
/// </summary>
public static class StockOutPermissions
{
    /// <summary>出库单列表查询</summary>
    public const string List = "stockOut:list";

    /// <summary>出库单详情</summary>
    public const string Detail = "stockOut:detail";

    /// <summary>创建出库单</summary>
    public const string Create = "stockOut:create";

    /// <summary>编辑出库单</summary>
    public const string Update = "stockOut:update";

    /// <summary>删除出库单</summary>
    public const string Delete = "stockOut:delete";
}
