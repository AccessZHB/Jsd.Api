namespace Jsd.Api.Models.Cms;

/// <summary>
/// 内容管理（公告）权限标识常量（与 sys_menu.permission 一一对应）。
/// 小程序端公告读取走 [AllowAnonymous]，不受此权限约束。
/// </summary>
public static class CmsPermissions
{
    public const string AnnouncementList   = "announcement:list";
    public const string AnnouncementAdd    = "announcement:add";
    public const string AnnouncementEdit   = "announcement:edit";
    public const string AnnouncementRemove = "announcement:delete";  // 历史命名 delete
    public const string AnnouncementStatus = "announcement:status";  // 发布/草稿、置顶
}
