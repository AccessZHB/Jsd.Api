using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Repositories;

/// <summary>
/// 购物车仓储实现（mem_cart）
/// </summary>
public class CartRepository : Repository<MemCart>, ICartRepository
{
    public CartRepository(AppDbContext db) : base(db)
    {
    }

    /// <inheritdoc />
    public async Task<List<MemCart>> GetListByMemberAsync(long memberId)
    {
        return await Db.MemCarts
            .AsNoTracking()
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<MemCart?> GetByIdAsync(long id, long memberId)
    {
        // 需要修改，保留 EF 跟踪
        return await Db.MemCarts
            .FirstOrDefaultAsync(c => c.Id == id && c.MemberId == memberId);
    }

    /// <inheritdoc />
    public async Task<MemCart?> FindByCartKeyAsync(string cartKey)
    {
        // 需要修改数量，保留 EF 跟踪
        return await Db.MemCarts.FirstOrDefaultAsync(c => c.CartKey == cartKey);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByMemberAsync(long memberId)
    {
        return await Db.MemCarts
            .Where(c => c.MemberId == memberId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task<int> SetSelectedAsync(long memberId, bool selected, long? id = null)
    {
        var flag = selected ? 1 : 0;
        var query = Db.MemCarts.Where(c => c.MemberId == memberId);
        if (id.HasValue)
        {
            query = query.Where(c => c.Id == id.Value);
        }
        return await query.ExecuteUpdateAsync(s => s.SetProperty(c => c.IsSelected, flag));
    }
}
