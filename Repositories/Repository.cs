using System.Linq.Expressions;
using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 泛型仓储实现（基于 EF Core）
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    /// <summary>数据库上下文，子类仓储（如 SysUserRepository）也可直接使用</summary>
    protected readonly AppDbContext Db;

    public Repository(AppDbContext db)
    {
        Db = db;
    }

    public async Task<T?> GetByIdAsync(long id)
    {
        return await Db.Set<T>().FindAsync(id);
    }

    public async Task<List<T>> GetListAsync()
    {
        // AsNoTracking：只读查询，不被 EF 跟踪，性能更好
        return await Db.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate)
    {
        return await Db.Set<T>().AsNoTracking().Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        // 注意：这里不加 AsNoTracking，登录后要修改用户信息，需要 EF 跟踪
        return await Db.Set<T>().FirstOrDefaultAsync(predicate);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await Db.Set<T>().AnyAsync(predicate);
    }

    public async Task AddAsync(T entity)
    {
        await Db.Set<T>().AddAsync(entity);
    }

    public void Update(T entity)
    {
        Db.Set<T>().Update(entity);
    }

    public void Remove(T entity)
    {
        Db.Set<T>().Remove(entity);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await Db.SaveChangesAsync();
    }

    public IQueryable<T> Query()
    {
        return Db.Set<T>();
    }
}
