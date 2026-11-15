namespace Jsd.Api.Models.File;

/// <summary>
/// 文件上传权限标识常量（与 sys_menu.permission 一一对应）。
///
/// 说明：/api/file/upload 与 /api/admin/upload/image 共用同一权限码。
/// 上传本身只做扩展名 + 文件头魔数 + 大小校验，但仍需显式授权，
/// 以便出现滥用（刷存储）时可按角色单独收回。
/// </summary>
public static class FilePermissions
{
    public const string Upload = "file:upload";
}
