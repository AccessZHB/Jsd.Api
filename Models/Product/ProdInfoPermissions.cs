namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品（SPU）管理权限标识常量（与 sys_menu.permission 一一对应）。
///
/// 命名约定：prod:info:{动作}
///   list   —— 后台商品列表（小程序商品浏览走 [AllowAnonymous]，不受此权限约束）
///   query  —— 详情 / 表单回显
///   add    —— 新增（聚合保存 SPU + 规格 + SKU + 图片）
///   edit   —— 修改
///   status —— 快速上下架（独立权限，便于只给运营上下架、不给改价）
///   remove —— 删除（级联清理 SKU/规格/图片）
/// </summary>
public static class ProdInfoPermissions
{
    public const string List   = "prod:info:list";
    public const string Query  = "prod:info:query";
    public const string Add    = "prod:info:add";
    public const string Edit   = "prod:info:edit";
    public const string Status = "prod:info:status";
    public const string Remove = "prod:info:remove";
}
