using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 用户仓储接口（在泛型仓储基础上，扩展用户特有的查询）
/// </summary>
public interface ISysUserRepository : IRepository<SysUser>
{
    /// <summary>
    /// 根据登录账号查询用户（同时带出角色信息）
    /// 用于登录校验。
    /// </summary>
    Task<SysUser?> GetByUserNameAsync(string userName);

    /// <summary>
    /// 根据ID查询用户详情（同时带出角色信息）
    /// 用于详情接口 / 修改表单回显。
    /// </summary>
    Task<SysUser?> GetWithRoleAsync(long id);

    /// <summary>
    /// 分页查询用户列表（含角色名称）
    /// </summary>
    /// <param name="keyword">搜索关键词（账号/姓名/手机号，可空）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>(当前页数据, 总条数)</returns>
    Task<(List<SysUser> Items, int Total)> GetPagedListAsync(string? keyword, int page, int pageSize);

    /// <summary>
    /// 分页查询当前仍处于锁定状态的账号（lock_until > 当前时间；系统管理模块·登录安全）
    /// </summary>
    Task<(List<SysUser> Items, int Total)> GetLockedAccountsAsync(string? memberName, string? ipAddress, int page, int pageSize);
}