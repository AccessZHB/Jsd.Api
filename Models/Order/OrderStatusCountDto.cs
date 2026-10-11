namespace Jsd.Api.Models.Order;

/// <summary>
/// 会员「我的订单」各状态数量统计（GET /api/order/status-count）。
///
/// 字段名按 camelCase 输出（与系统其余接口一致）：
///   pendingPay    待付款（OrderStatuses.Pending = 0）
///   pendingShip   待发货 / 已支付（OrderStatuses.Paid = 1）
///   pendingReceive 待收货 / 已发货（OrderStatuses.Shipped = 2）
///   completed     已完成（OrderStatuses.Completed = 3）
///   closed        已关闭（OrderStatuses.Closed = 4）
///
/// 后端先按当前登录会员（JWT member_id → buyer_id）强制归属过滤，再按 order_status 分组计数，
/// 不存在的状态返回 0，绝不返回 null。
/// </summary>
public class OrderStatusCountDto
{
    /// <summary>待付款数量（OrderStatuses.Pending = 0）</summary>
    public int PendingPay { get; set; }

    /// <summary>待发货 / 已支付数量（OrderStatuses.Paid = 1）</summary>
    public int PendingShip { get; set; }

    /// <summary>待收货 / 已发货数量（OrderStatuses.Shipped = 2）</summary>
    public int PendingReceive { get; set; }

    /// <summary>已完成数量（OrderStatuses.Completed = 3）</summary>
    public int Completed { get; set; }

    /// <summary>已关闭数量（OrderStatuses.Closed = 4）</summary>
    public int Closed { get; set; }
}
