using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 订单管理接口
/// 路由前缀：/api/order
/// 权限标识见 <see cref="OrderPermissions"/>（order:list / detail / remark / ship / export / create / pay / complete / cancel）
///
/// 接口清单：
///   1. GET    /api/order/page              分页查询订单（订单号/状态/时间范围 + 商品总数）
///   2. GET    /api/order/{id}/detail       订单详情（主表 + trx_order_item 明细）
///   3. PUT    /api/order/{id}/remark       修改订单备注（仅 待付款 可改）
///   4. PUT    /api/order/{id}/ship         手动发货（仅 已支付 可发货）
///   5. GET    /api/order/export            订单导出（占位实现）
///   —— 以下为支撑"库存/销量联动"的补充接口 ——
///   6. POST   /api/order                   创建订单（生成订单号 + 商品快照 + 事务）
///   7. PUT    /api/order/{id}/pay          支付（待付款→已支付，【扣减库存】）
///   8. PUT    /api/order/{id}/complete     确认完成（已发货→已完成，【增加销量】）
///   9. PUT    /api/order/{id}/cancel       关闭订单（待付款→已关闭）
///
/// 状态流转（无审核，支付即生效）：
///   待付款(0) ─支付[扣库存]─&gt; 已支付(1) ─发货─&gt; 已发货(2) ─完成[加销量]─&gt; 已完成(3)
///   待付款(0) ─关闭─&gt; 已关闭(4)
/// </summary>
[ApiController]
[Route("api/order")]
[Authorize]  // 必须登录
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // ============================================================
    // 1. 分页查询订单列表
    // ============================================================

    /// <summary>
    /// 1. 分页查询订单列表（权限：order:list）
    /// 返回订单主表信息 + 商品总数（明细行数 ItemCount / 商品件数合计 TotalQuantity）。
    /// 示例：GET /api/order/page?orderNo=ORD2026&amp;orderStatus=1&amp;startTime=2026-09-01&amp;endTime=2026-09-12&amp;page=1&amp;pageSize=10
    /// </summary>
    /// <param name="orderNo">订单号关键词（模糊，可空）</param>
    /// <param name="orderStatus">订单状态：0-待付款 1-已支付 2-已发货 3-已完成 4-已关闭（可空=全部）</param>
    /// <param name="startTime">下单时间起（可空）</param>
    /// <param name="endTime">下单时间止（可空，只传日期自动按当天结束处理）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("page")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrderListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<OrderListDto>>> GetPage(
        [FromQuery] string? orderNo,
        [FromQuery] int? orderStatus,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _orderService.GetPagedListAsync(
            orderNo, orderStatus, startTime, endTime, page, pageSize);
    }

    // ============================================================
    // 2. 订单详情
    // ============================================================

    /// <summary>
    /// 2. 获取订单详情（权限：order:detail）
    /// 返回订单主表信息 + 订单明细列表（trx_order_item，名称/规格/单价为下单快照）。
    /// 示例：GET /api/order/1/detail
    /// </summary>
    /// <param name="id">订单ID</param>
    [HttpGet("{id:long}/detail")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<OrderDetailDto>> GetDetail(long id)
    {
        return await _orderService.GetDetailAsync(id);
    }

    // ============================================================
    // 3. 修改订单备注
    // ============================================================

    /// <summary>
    /// 3. 修改订单备注（权限：order:remark）
    /// 【状态校验】只有 待付款(0) 订单可修改（与修改收货地址同规则）。
    /// 修改的是 merchant_remark（商家备注，后台内部可见）。
    /// 示例：PUT /api/order/1/remark   body: { "merchantRemark": "客户要求发顺丰" }
    /// </summary>
    /// <param name="id">订单ID</param>
    [HttpPut("{id:long}/remark")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> UpdateRemark(long id, [FromBody] OrderRemarkDto dto)
    {
        return await _orderService.UpdateRemarkAsync(id, dto);
    }

    // ============================================================
    // 4. 手动发货
    // ============================================================

    /// <summary>
    /// 4. 手动发货（权限：order:ship）
    /// 【状态校验】只有 已支付(1) 订单可发货；发货后状态置为 已发货(2)。
    /// 示例：PUT /api/order/1/ship   body: { "shipCompany": "顺丰速运", "shipNo": "SF1234567890" }
    /// </summary>
    /// <param name="id">订单ID</param>
    [HttpPut("{id:long}/ship")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Ship(long id, [FromBody] OrderShipDto dto)
    {
        return await _orderService.ShipAsync(id, dto);
    }

    // ============================================================
    // 5. 订单导出（占位）
    // ============================================================

    /// <summary>
    /// 5. 订单导出（权限：order:export）—— 占位实现
    /// 接收与列表查询相同的筛选参数，目前不生成真实 Excel，
    /// 仅返回提示信息与命中的订单条数（ExportUrl 为 null）。
    /// 示例：GET /api/order/export?orderStatus=3&amp;startTime=2026-09-01
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(ApiResponse<OrderExportDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<OrderExportDto>> Export(
        [FromQuery] string? orderNo,
        [FromQuery] int? orderStatus,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        return await _orderService.ExportAsync(orderNo, orderStatus, startTime, endTime);
    }

    // ============================================================
    // 以下为支撑"库存/销量联动"的补充接口
    // （若暂时不需要，可直接删除这一段，不影响上面 5 个接口）
    // ============================================================

    /// <summary>
    /// 6. 创建订单（权限：order:create）
    /// 后端生成订单号、快照商品信息、重算金额，初始状态 = 待付款(0)。
    /// 【注意】创建阶段不扣库存，库存扣减发生在支付时。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<object>> Create([FromBody] OrderCreateDto dto)
    {
        // DTO 特性做声明式校验；商品/SKU 不存在等业务校验由 Service 抛
        // InvalidOperationException，在此统一转 400
        try
        {
            return await _orderService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 7. 订单支付（权限：order:pay）—— 支付即生效，无需人工审核
    /// 状态 待付款(0) → 已支付(1)，同时【扣减各 SKU 库存】并写变动日志，全程事务。
    /// 示例：PUT /api/order/1/pay   body: { "payMethod": 1 }
    /// </summary>
    [HttpPut("{id:long}/pay")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Pay(long id, [FromBody] OrderPayDto dto)
    {
        try
        {
            return await _orderService.PayAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            // 库存不足 / 并发冲突等业务异常统一转 400
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 8. 确认完成（权限：order:complete）
    /// 状态 已发货(2) → 已完成(3)，同时【增加各 SKU 销量】，全程事务。
    /// 注意：此步不再扣库存（库存已在支付时扣减）。
    /// </summary>
    [HttpPut("{id:long}/complete")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Complete(long id)
    {
        return await _orderService.CompleteAsync(id);
    }

    /// <summary>
    /// 9. 关闭订单（权限：order:cancel）
    /// 状态 待付款(0) → 已关闭(4)，记录关闭原因与时间。
    /// 已支付订单需走退款流程，本接口不处理。
    /// </summary>
    [HttpPut("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Cancel(long id, [FromBody] OrderCloseDto? dto)
    {
        return await _orderService.CancelAsync(id, dto ?? new OrderCloseDto());
    }
}
