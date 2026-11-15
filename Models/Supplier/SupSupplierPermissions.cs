namespace Jsd.Api.Models.Supplier;

/// <summary>
/// 供应商管理权限标识常量（与 sys_menu.permission 一一对应）。
///
/// 命名约定：sup:supplier:{动作}
///   list  —— 分页列表
///   query —— 详情 / 启用供应商下拉（含 getAll）
///   add / edit / remove —— 增改删
/// </summary>
public static class SupSupplierPermissions
{
    public const string List   = "sup:supplier:list";
    public const string Query  = "sup:supplier:query";
    public const string Add    = "sup:supplier:add";
    public const string Edit   = "sup:supplier:edit";
    public const string Remove = "sup:supplier:remove";
}
