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

    public BalanceController(IBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>查询会员余额概况（可用 / 冻结 / 累计充值 / 等级）</summary>
    [HttpGet("{memberId:long}")]
    public async Task<ApiResponse<MemberBalanceDto>> GetMemberBalance(long memberId)
    {
        return await _balanceService.GetMemberBalanceAsync(memberId);
    }

    /// <summary>分页查询余额流水（会员 / 变动类型 / 关联业务 / 时间范围）</summary>
    [HttpGet("logs")]
    public async Task<ApiResponse<PagedResult<BalanceLogDto>>> GetLogs([FromQuery] BalanceLogQueryDto query)
    {
        return await _balanceService.GetLogsAsync(query);
    }

    /// <summary>后台手工调整余额（管理员权限，记录 operator_id 与原因）</summary>
    [HttpPost("admin-adjust")]
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
