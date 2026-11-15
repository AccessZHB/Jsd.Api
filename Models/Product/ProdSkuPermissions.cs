namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品 SKU 管理权限标识常量（与 sys_menu.permission 一一对应）。
///
/// 命名约定：prod:sku:{动作}
///   价格（retail_price / sale_price）与库存（stock）都在 SKU 上，
///   因此 prod:sku:edit 等价于"改价 + 改库存"，属资金相关高危权限。
/// </summary>
public static class ProdSkuPermissions
{
    public const string List   = "prod:sku:list";
    public const string Query  = "prod:sku:query";
    public const string Add    = "prod:sku:add";
    public const string Edit   = "prod:sku:edit";
    public const string Remove = "prod:sku:remove";
}
