using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 会员余额接口（价格策略与会员余额模块）
/// 路由前缀：/api/balance
///
/// 【资金安全】冻结 / 解冻 / 扣减 / 调整全部在事务内完成，并写 mkt_balance_log 台账，
/// before_balance / after_balance 与 mem_member 实时余额严格勾稽。
/// 【并发】采用 CAS 乐观锁（UPDATE ... WHERE balance=@before）防止超扣。
/// </summary>
[ApiController]
[Route("api/balance")]
[Authorize]
public class BalanceController : ControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly CurrentUserService _currentUser;

    public BalanceController(IBalanceService balanceService, CurrentUserService currentUser)
    {
        _balanceService = balanceService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 查询会员余额概况（可用 / 冻结 / 累计充值 / 等级）
    ///
    /// 【阶段六】归属校验：会员令牌只能查自己的余额。
    /// 原实现仅 [Authorize]，会员把 URL 里的 memberId 改成别人的就能查他人资金信息。
    /// 管理员令牌（无 member_id 声明）仍可查任意会员，供后台客服使用。
    /// </summary>
    [HttpGet("{memberId:long}")]
    public async Task<ApiResponse<MemberBalanceDto>> GetMemberBalance(long memberId)
    {
        if (_currentUser.IsMember && memberId != _currentUser.MemberId)
        {
            return ApiResponse<MemberBalanceDto>.Fail("无权查看他人账户余额", 403);
        }

        return await _balanceService.GetMemberBalanceAsync(memberId);
    }

    /// <summary>
    /// 【P0-3 修复】分页查询余额流水。
    /// 安全：必须具备 finance:balance:log 权限；会员令牌强制只查自己的流水，
    ///       绝不允许空条件导出全平台资金流水。
    /// </summary>
    [HttpGet("logs")]
    [Permission(RechargePermissions.BalanceLog)]
    public async Task<ApiResponse<PagedResult<BalanceLogDto>>> GetLogs([FromQuery] BalanceLogQueryDto query)
    {
        // 会员令牌：强制归属当前会员，覆盖前端传值，杜绝越权查看他人流水
        if (_currentUser.IsMember)
        {
            query.MemMemberId = _currentUser.MemberId;
            query.Keyword = null;   // 会员不允许按他人姓名/编号模糊查询
        }

        return await _balanceService.GetLogsAsync(query);
    }

    /// <summary>后台手工调整余额（管理员权限，记录 operator_id 与原因）</summary>
    [HttpPost("admin-adjust")]
    [SuperAdminOnly(Operation = "余额调整")]
    public async Task<ApiResponse<bool>> AdminAdjust([FromBody] AdminAdjustBalanceDto dto)
    {
        try
        {
            return await _balanceService.AdminAdjustAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>下单冻结：校验 balance ≥ 金额，balance -= 、frozen_balance += ，写冻结流水</summary>
    [HttpPost("freeze")]
    [SuperAdminOnly(Operation = "余额冻结")]
    public async Task<ApiResponse<bool>> Freeze([FromBody] FreezeBalanceDto dto)
    {
        try
        {
            return await _balanceService.FreezeAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>取消/退款解冻：frozen_balance -= 、balance += ，写解冻流水</summary>
    [HttpPost("unfreeze")]
    [SuperAdminOnly(Operation = "余额解冻")]
    public async Task<ApiResponse<bool>> Unfreeze([FromBody] UnfreezeBalanceDto dto)
    {
        try
        {
            return await _balanceService.UnfreezeAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>下单扣减：frozen_balance -= 金额，写扣减流水</summary>
    [HttpPost("deduct")]
    [SuperAdminOnly(Operation = "余额扣减")]
    public async Task<ApiResponse<bool>> Deduct([FromBody] DeductBalanceDto dto)
    {
        try
        {
            return await _balanceService.DeductAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }
}
