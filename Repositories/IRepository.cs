using System.Linq.Expressions;

namespace Jsd.Api.Repositories;

/// <summary>
/// 泛型仓储接口：封装最常用的数据库操作，
/// 让 Service 层不直接接触 DbContext（分层架构）。
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>根据主键查询</summary>
    Task<T?> GetByIdAsync(long id);

    /// <summary>查询全部数据</summary>
    Task<List<T>> GetListAsync();

    /// <summary>按条件查询</summary>
    Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate);

    /// <summary>按条件查询单条记录</summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    /// <summary>判断是否存在满足条件的数据</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

    /// <summary>新增</summary>
    Task AddAsync(T entity);

    /// <summary>修改（EF 跟踪机制下，调用 SaveChanges 时自动更新）</summary>
    void Update(T entity);

    /// <summary>删除</summary>
    void Remove(T entity);

    /// <summary>保存变更到数据库</summary>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// 获取可继续拼接 LINQ 的查询对象（用于 Include / OrderBy / 分页等复杂查询）
    /// </summary>
    IQueryable<T> Query();
}
