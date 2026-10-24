using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 会员端售后接口（小程序「我的订单 → 退款/售后」）
/// 路由前缀：/api/mp/after-sales
///
/// 与后台 /api/after-sale/refund 的区别：
///   1. 仅接受【会员令牌】（JWT 带 member_id）；管理员令牌调用直接 401，避免身份串用；
///   2. memberId 一律由后端从令牌解析，绝不信任前端传参（防越权申请他人订单售后）；
///   3. 放宽订单状态：已支付(1)/已发货(2)/已完成(3) 均可申请（后台仅限已完成）；
///   4. 申请成功后订单进入 5-售后中，审核通过退款后关闭订单，驳回则恢复原状态。
///
/// 状态流转（售后单 trx_refund.status）：
///   0 待审核（小程序展示「售后审核中」）
///     ├─ 审核通过（仅退款）→ 自动退款 + 关闭订单 → 2 已退款（「退款成功」）
///     ├─ 审核通过（退货退款）→ 1 待退款（「退款中」，需仓库收货后再打款）
///     └─ 驳回 → 恢复订单原状 → 3 已驳回（「售后关闭」）
/// </summary>
[ApiController]
[Route("api/mp/after-sales")]
[Authorize]
public class MpAfterSaleController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly CurrentUserService _currentUser;

    public MpAfterSaleController(IRefundService refundService, CurrentUserService currentUser)
    {
        _refundService = refundService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 会员提交售后/退款申请。
    /// 示例：POST /api/mp/after-sales/apply
    /// body: { "orderId": 123, "refundAmount": 99.00, "refundType": 1, "refundReason": "镜片度数不合适", "voucherImages": "/upload/a.jpg,/upload/b.jpg" }
    /// </summary>
    /// <remarks>refundType：1-仅退款 2-退货退款（需寄回实物）</remarks>
    [HttpPost("apply")]
    [ProducesResponseType(typeof(ApiResponse<RefundCreateResult>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<RefundCreateResult>> Apply([FromBody] MpApplyRefundDto dto)
    {
        // 必须是会员令牌：管理员令牌不含 member_id，直接拒绝（与 /api/order/my 同策略）
        if (!_currentUser.IsMember || _currentUser.MemberId <= 0)
        {
            return ApiResponse<RefundCreateResult>.Fail("请使用会员身份登录", 401);
        }

        try
        {
            return await _refundService.ApplyByMemberAsync(dto, _currentUser.MemberId);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<RefundCreateResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 会员售后/退款列表（小程序「退款/售后」Tab 数据源）。
    /// 仅返回当前登录会员本人的售后单，按申请时间倒序，附带订单商品摘要与状态文案。
    /// 示例：GET /api/mp/after-sales/list?page=1&amp;pageSize=10
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MpRefundListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<MpRefundListItemDto>>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!_currentUser.IsMember || _currentUser.MemberId <= 0)
        {
            return ApiResponse<PagedResult<MpRefundListItemDto>>.Fail("请使用会员身份登录", 401);
        }

        return await _refundService.GetMyListAsync(_currentUser.MemberId, page, pageSize);
    }
}
