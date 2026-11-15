namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品规格（规格项 + 规格值）权限标识常量（与 sys_menu.permission 一一对应）。
///
/// 说明：规格项（颜色/尺码/折射率…）与规格值（红/蓝/M/1.56…）属于同一业务对象，
///       共用一套权限，避免在菜单里出现两套高度重复的按钮授权。
///       命名约定：prod:spec:{动作}
/// </summary>
public static class ProdSpecPermissions
{
    public const string List   = "prod:spec:list";
    public const string Add    = "prod:spec:add";
    public const string Edit   = "prod:spec:edit";
    public const string Remove = "prod:spec:remove";
}
