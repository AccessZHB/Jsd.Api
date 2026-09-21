using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;

namespace Jsd.Api.Repositories;

/// <summary>
/// 退货单 仓储接口（继承泛型仓储，扩展分页/按退款单查询/单号生成）
/// </summary>
public interface IReturnRepository : IRepository<TrxReturn>
{
    /// <summary>分页查询退货列表（支持退货单号/订单号/会员/退款单/状态/时间范围筛选）</summary>
    Task<(List<TrxReturn> Items, int Total)> GetPagedListAsync(ReturnQueryDto q);

    /// <summary>带跟踪查询（收货时需要修改状态与明细）</summary>
    Task<TrxReturn?> GetByIdTrackedAsync(long id);

    /// <summary>根据退款单ID查询关联退货单</summary>
    Task<List<TrxReturn>> GetByRefundIdAsync(long refundId);

    /// <summary>退货单号是否已存在（唯一索引兜底前的预检）</summary>
    Task<bool> ExistsReturnNoAsync(string returnNo);

    /// <summary>生成退货单号候选（TH + yyyyMMddHHmmss + 4位随机）</summary>
    string GenerateReturnNo();
}
