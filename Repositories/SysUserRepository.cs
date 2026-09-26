using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 用户仓储实现
/// </summary>
public class SysUserRepository : Repository<SysUser>, ISysUserRepository
{
    public SysUserRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 根据登录账号查询用户（Include 角色，供登录后取角色编码生成 JWT）
    /// 注意：不加 AsNoTracking，登录成功后要回写最后登录时间/IP
    /// </summary>
    public async Task<SysUser?> GetByUserNameAsync(string userName)
    {
        return await Db.SysUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName);
    }

    /// <summary>
    /// 根据ID查询用户详情（Include 角色，用于详情接口回显角色名称）
    /// </summary>
    public async Task<SysUser?> GetWithRoleAsync(long id)
    {
        return await Db.SysUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// 分页查询用户列表（左连接角色表，带出角色名称）
    /// </summary>
    public async Task<(List<SysUser> Items, int Total)> GetPagedListAsync(string? keyword, int page, int pageSize)
    {
        // 基础查询：用户表 + 角色导航属性
        var query = Db.SysUsers
            .Include(u => u.Role)
            .AsNoTracking()
            .AsQueryable();

        // 关键词模糊搜索：账号 / 真实姓名 / 手机号
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(u =>
                u.UserName.Contains(keyword) ||
                u.RealName.Contains(keyword) ||
                (u.Phone != null && u.Phone.Contains(keyword)));
        }

        // 总条数（分页前统计）
        var total = await query.CountAsync();

        // 分页：按 Id 倒序（新用户在前），跳过前 N 条取 PageSize 条
        var items = await query
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    /// 分页查询当前仍处于锁定状态的账号（lock_until > 当前时间）
    /// </summary>
    public async Task<(List<SysUser> Items, int Total)> GetLockedAccountsAsync(string? memberName, string? ipAddress, int page, int pageSize)
    {
        var query = Db.SysUsers
            .AsNoTracking()
            .Where(u => u.LockUntil != null && u.LockUntil > DateTime.Now);

        if (!string.IsNullOrWhiteSpace(memberName))
            query = query.Where(u => u.UserName.Contains(memberName));
        if (!string.IsNullOrWhiteSpace(ipAddress))
            query = query.Where(u => u.LastLoginIp != null && u.LastLoginIp.Contains(ipAddress));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.LockUntil)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}