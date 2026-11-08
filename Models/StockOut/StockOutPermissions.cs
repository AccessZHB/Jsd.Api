namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 出库管理模块 —— 按钮级权限标识（菜单权限 Permission 字段取值）。
/// 与 sys_menu.permission 种子保持一致，由 [Permission] 注解在 Controller Action 上强制校验。
///
/// 5 个按钮级权限标识对照表：
/// ┌──────────────────────┬──────────────────────┬────────────────────────────────┐
/// │ 权限标识 (Permission) │ 对应接口             │ 说明                           │
/// ├──────────────────────┼──────────────────────┼────────────────────────────────┤
/// │ stock_out:list       │ GET  /api/stockOut/list    │ 出库单分页列表查询        │
/// │ stock_out:list       │ GET  /api/stockOut/detail/{id} │ 出库单详情（含明细）  │
/// │ stock_out:add        │ POST /api/stockOut/create    │ 创建出库单              │
/// │ stock_out:edit       │ PUT  /api/stockOut/update/{id} │ 编辑出库单            │
/// │ stock_out:delete     │ DELETE /api/stockOut/delete/{id} │ 删除出库单（回补库存）│
/// └──────────────────────┴──────────────────────┴────────────────────────────────┘
/// </summary>
public static class StockOutPermissions
{
    /// <summary>出库单列表查询</summary>
    public const string List = "stock_out:list";

    /// <summary>出库单详情（复用列表权限）</summary>
    public const string Detail = "stock_out:list";

    /// <summary>创建出库单</summary>
    public const string Create = "stock_out:add";

    /// <summary>编辑出库单</summary>
    public const string Update = "stock_out:edit";

    /// <summary>删除出库单</summary>
    public const string Delete = "stock_out:delete";
}
