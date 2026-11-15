namespace Jsd.Api.Models.Home;

/// <summary>
/// 首页轮播图（运营位）权限标识常量（与 sys_menu.permission 一一对应）。
/// 小程序端轮播读取走 [AllowAnonymous]，不受此权限约束。
/// </summary>
public static class BannerPermissions
{
    public const string List   = "banner:list";
    public const string Add    = "banner:add";
    public const string Edit   = "banner:edit";
    public const string Remove = "banner:delete";   // 历史命名 delete，保持与库一致
    public const string Status = "banner:status";   // 启用/禁用（独立授权）
}
