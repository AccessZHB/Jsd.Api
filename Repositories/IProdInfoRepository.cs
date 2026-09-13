using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 商品（SPU）仓储接口
/// </summary>
public interface IProdInfoRepository : IRepository<ProdInfo>
{
    /// <summary>
    /// 分页查询商品列表（关联分类名称、供应商名称，支持多条件筛选）
    /// </summary>
    /// <param name="keyword">商品名称关键词（可空）</param>
    /// <param name="categoryId">分类ID（可空）</param>
    /// <param name="supplierId">供应商ID（可空）</param>
    /// <param name="status">状态（可空：null=全部，1=上架，0=下架）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    Task<(List<ProdInfo> Items, int Total)> GetPagedListAsync(
        string? keyword, long? categoryId, long? supplierId, int? status, int page, int pageSize);

    /// <summary>
    /// 根据ID查询商品详情（Include 分类和供应商，用于表单回显）
    /// </summary>
    Task<ProdInfo?> GetWithDetailAsync(long id);
}
