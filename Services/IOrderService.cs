using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;

namespace Jsd.Api.Services;

/// <summary>
/// 订单服务接口。
///
/// 接口清单（路由前缀 /api/order）：
///   GET    /api/order/page            分页查询订单（订单号/状态/时间范围筛选 + 商品总数）
///   GET    /api/order/{id}/detail     订单详情（主表 + 明细列表）
///   PUT    /api/order/{id}/remark     修改订单备注（仅 待付款 可改）
///   PUT    /api/order/{id}/ship       手动发货（仅 已支付 可发货）
///   GET    /api/order/export          订单导出（占位实现）
///   —— 以下为支撑"库存/销量联动"的补充接口 ——
///   POST   /api/order                 创建订单（生成订单号 + 商品快照 + 事务）
///   PUT    /api/order/{id}/pay        支付（待付款→已支付，【扣减 SKU 库存】）
///   PUT    /api/order/{id}/complete   确认完成（已发货→已完成，【增加 SKU 销量】）
///   PUT    /api/order/{id}/cancel     关闭订单（待付款→已关闭）
///
/// 权限：order:list / detail / remark / ship / export / create / pay / complete / cancel
/// </summary>
public interface IOrderService
{
    /// <summary>分页查询订单列表（主表信息 + 商品总数）</summary>
    Task<ApiResponse<PagedResult<OrderListDto>>> GetPagedListAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime, int page, int pageSize);

    /// <summary>订单详情（主表 + 明细）</summary>
    Task<ApiResponse<OrderDetailDto>> GetDetailAsync(long id);

    /// <summary>创建订单（生成订单号、快照商品信息、计算金额，全程事务）</summary>
    Task<ApiResponse<object>> CreateAsync(OrderCreateDto dto);

    /// <summary>支付（待付款 → 已支付，扣减 SKU 库存并写变动日志，全程事务）</summary>
    Task<ApiResponse<object>> PayAsync(long id, OrderPayDto dto);

    /// <summary>手动发货（已支付 → 已发货，填写物流公司/单号）</summary>
    Task<ApiResponse<object>> ShipAsync(long id, OrderShipDto dto);

    /// <summary>确认完成（已发货 → 已完成，增加 SKU 销量，全程事务）</summary>
    Task<ApiResponse<object>> CompleteAsync(long id);

    /// <summary>关闭订单（待付款 → 已关闭）</summary>
    Task<ApiResponse<object>> CancelAsync(long id, OrderCloseDto dto);

    /// <summary>修改商家备注（仅 待付款 可改）</summary>
    Task<ApiResponse<object>> UpdateRemarkAsync(long id, OrderRemarkDto dto);

    /// <summary>订单导出（占位实现：不生成真实文件，返回提示与命中条数）</summary>
    Task<ApiResponse<OrderExportDto>> ExportAsync(
        string? orderNo, int? orderStatus, DateTime? startTime, DateTime? endTime);

    /// <summary>确认订单页预计算：重算金额 + 逐 SKU 库存校验（不改库，会员隔离）</summary>
    Task<ApiResponse<OrderPreCalculateResultDto>> PreCalculateAsync(OrderPreCalculateDto dto);

    /// <summary>
    /// 单事务提交订单：再校验库存 → 建单(待支付) → 余额直扣 → 置已支付。
    /// 仅支持会员余额支付；余额不足直接拒绝（不落库）。
    /// </summary>
    Task<ApiResponse<OrderSubmitResultDto>> SubmitAsync(OrderSubmitDto dto);

    /// <summary>
    /// 会员「我的订单」：仅查询当前登录会员（JWT member_id）名下的订单，
    /// 在 EF 查询中强制追加 .Where(o => o.BuyerId == 当前会员ID) 过滤条件。
    /// 严禁信任前端传入的 userId；会员身份一律从 Token 解析。
    /// </summary>
    Task<ApiResponse<PagedResult<OrderListDto>>> GetMyOrdersAsync(
        int? orderStatus, DateTime? startTime, DateTime? endTime, int page, int pageSize);
}
