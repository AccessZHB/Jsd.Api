using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysUser;

namespace Jsd.Api.Services;

/// <summary>
/// 系统用户服务接口
/// </summary>
public interface ISysUserService
{
    /// <summary>
    /// 分页查询用户列表（含角色名称）
    /// </summary>
    Task<ApiResponse<PagedResult<SysUserListDto>>> GetPagedListAsync(string? keyword, int page, int pageSize);

    /// <summary>
    /// 新增用户（密码 BCrypt 加密入库）
    /// </summary>
    Task<ApiResponse<object>> CreateAsync(SysUserCreateDto dto);

    /// <summary>
    /// 根据 ID 获取用户详情（用于修改表单回显）
    /// </summary>
    Task<ApiResponse<SysUserListDto>> GetByIdAsync(long id);

    /// <summary>
    /// 修改用户信息（不支持修改密码）
    /// </summary>
    Task<ApiResponse<object>> UpdateAsync(SysUserUpdateDto dto);

    /// <summary>
    /// 根据 ID 删除用户（物理删除，含业务保护）
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    /// <summary>
    /// 批量删除用户
    /// </summary>
    Task<ApiResponse<object>> DeleteBatchAsync(long[] ids);
}
