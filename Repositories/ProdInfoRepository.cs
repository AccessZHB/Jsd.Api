using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 商品（SPU）仓储实现
/// </summary>
public class ProdInfoRepository : Repository<ProdInfo>, IProdInfoRepository
{
    public ProdInfoRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询商品列表
    /// 使用 Include 一次查询带出分类名称、供应商名称，避免前端二次请求（规范 5.6）
    /// </summary>
    public async Task<(List<ProdInfo> Items, int Total)> GetPagedListAsync(
        string? keyword, long? categoryId, long? supplierId, int? status, int page, int pageSize)
    {
        // 基础查询：商品 + 分类导航 + 供应商导航（生成的 SQL 为 LEFT JOIN，不会丢弃分类/供应商被禁用的商品）
        var query = Db.ProdInfos
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsNoTracking()
            .AsQueryable();

        // 商品名称模糊搜索（对应数据库 prod_info_name 列）
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.ProdInfoName.Contains(keyword));
        }

        // 分类筛选（精确匹配所选分类）
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.ProdCategoryId == categoryId.Value);
        }

        // 供应商筛选
        if (supplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }

        // 状态筛选（null = 不过滤）
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        // 总条数（分页前统计）
        var total = await query.CountAsync();

        // 分页：按 Id 倒序（新商品在前）
        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    /// 根据ID查询商品详情（Include 分类和供应商）
    /// </summary>
    public async Task<ProdInfo?> GetWithDetailAsync(long id)
    {
        return await Db.ProdInfos
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
