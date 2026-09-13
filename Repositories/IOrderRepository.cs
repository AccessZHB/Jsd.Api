using Jsd.Api.Entities;

namespace Jsd.Api.Repositories;

/// <summary>
/// 订单主表仓储接口（继承泛型仓储，额外提供分页查询、详情、订单号查重）
/// </summary>
public interface IOrderRepository : IRepository<TrxOrder>
{
    /// <summary>
    /// 分页查询订单（订单号模糊 / 状态 / 创建时间范围 筛选；默认过滤 is_deleted=0）
    /// </summary>
    /// <param name="orderNo">订单号关键词（模糊，可空）</param>
    /// <param name="orderStatus">订单状态：0-待付款 1-已支付 2-已发货 3-已完成 4-已关闭（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空，只传日期自动按当天 23:59:59 处理）</param>
    Task<(List<TrxOrder> Items, int Total)> GetPagedListAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>根据ID查询订单详情（含明细，明细按 Id 升序）</summary>
    Task<TrxOrder?> GetDetailAsync(long id);

    /// <summary>订单号是否已存在（uk_order_no 唯一索引的业务层兜底校验）</summary>
    Task<bool> OrderNoExistsAsync(string orderNo);
}
