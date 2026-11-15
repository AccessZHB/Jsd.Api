namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品分类权限标识常量（与 sys_menu.permission 一一对应）。
/// 分类树 /tree 供小程序公开浏览，走 [AllowAnonymous]，不受此权限约束。
/// </summary>
public static class ProdCategoryPermissions
{
    public const string List   = "prod:category:list";
    public const string Add    = "prod:category:add";
    public const string Edit   = "prod:category:edit";
    public const string Remove = "prod:category:remove";
}
