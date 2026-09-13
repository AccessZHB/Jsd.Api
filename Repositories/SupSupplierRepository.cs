using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 供应商仓储实现
/// </summary>
public class SupSupplierRepository : Repository<SupSupplier>, ISupSupplierRepository
{
    public SupSupplierRepository(AppDbContext db) : base(db)
    {
    }

    /// <summary>
    /// 分页查询供应商列表（名称模糊搜索 + 状态筛选）
    /// </summary>
    public async Task<(List<SupSupplier> Items, int Total)> GetPagedListAsync(string? keyword, int? status, int page, int pageSize)
    {
        var query = Db.SupSuppliers.AsNoTracking().AsQueryable();

        // 名称模糊搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(s => s.SupplierName.Contains(keyword));
        }

        // 状态筛选（null = 不过滤）
        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        // 总条数（分页前统计）
        var total = await query.CountAsync();

        // 分页：按 Id 倒序（新供应商在前）
        var items = await query
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
