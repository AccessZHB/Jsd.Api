using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 供应商仓储接口
/// </summary>
public interface ISupSupplierRepository : IRepository<SupSupplier>
{
    /// <summary>
    /// 分页查询供应商列表（支持名称模糊搜索 + 状态筛选）
    /// </summary>
    /// <param name="keyword">供应商名称关键词（可空）</param>
    /// <param name="status">状态筛选（可空：null=全部，1=启用，0=禁用）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>(当前页数据, 总条数)</returns>
    Task<(List<SupSupplier> Items, int Total)> GetPagedListAsync(string? keyword, int? status, int page, int pageSize);
}
