using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.SysRole;

/// <summary>
/// 角色列表项 DTO
/// </summary>
public class SysRoleDto
{
    public long Id { get; set; }

    /// <summary>角色名称（如：运营专员）</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（如：operation）</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>数据权限：1-全部 2-本部门 3-仅本人</summary>
    public int DataScope { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 修改角色请求 DTO
/// </summary>
public class SysRoleUpdateDto
{
    /// <summary>角色ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "角色ID不合法")]
    public long Id { get; set; }

    /// <summary>角色名称</summary>
    [Required(ErrorMessage = "角色名称不能为空")]
    [StringLength(50, ErrorMessage = "角色名称最长50个字符")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（唯一）</summary>
    [Required(ErrorMessage = "角色编码不能为空")]
    [StringLength(30, ErrorMessage = "角色编码最长30个字符")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>数据权限：1-全部 2-本部门 3-仅本人</summary>
    public int DataScope { get; set; } = 1;

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(255)]
    public string? Remark { get; set; }
}

/// <summary>
/// 给角色分配菜单权限请求 DTO（PUT /api/sys/role/auth）
/// </summary>
public class SysRoleAuthDto
{
    /// <summary>角色ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "角色ID不合法")]
    public long RoleId { get; set; }

    /// <summary>授权的菜单ID列表（目录/菜单/按钮的 Id 集合；传空数组 = 清空该角色全部权限）</summary>
    public List<long> MenuIds { get; set; } = new();
}
