using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 客户（会员）等级接口（价格策略与会员余额模块）
/// 路由前缀：/api/member-levels
///
/// 【停用约束】停用等级前会校验是否仍被「启用中且生效期内」的价格策略引用，
/// 防止客户下单时价格静默跳回原价。
/// </summary>
[ApiController]
[Route("api/member-levels")]
[Authorize]
public class MemberLevelController : ControllerBase
{
    private readonly IPriceService _priceService;

    public MemberLevelController(IPriceService priceService)
    {
        _priceService = priceService;
    }

    /// <summary>分页查询客户等级（支持状态、关键字筛选）</summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<MemberLevelDto>>> GetPage([FromQuery] MemberLevelQueryDto query)
    {
        return await _priceService.GetLevelsAsync(query);
    }

    /// <summary>新增客户等级（level_code 唯一）</summary>
    [HttpPost]
    public async Task<ApiResponse<long>> Create([FromBody] CreateMemberLevelDto dto)
    {
        try
        {
            return await _priceService.CreateLevelAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>更新客户等级</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResponse<bool>> Update(long id, [FromBody] UpdateMemberLevelDto dto)
    {
        try
        {
            return await _priceService.UpdateLevelAsync(id, dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>启用/停用客户等级</summary>
    [HttpPatch("{id:long}/status")]
    public async Task<ApiResponse<bool>> UpdateStatus(long id, [FromBody] MemberLevelStatusDto dto)
    {
        return await _priceService.UpdateLevelStatusAsync(id, dto);
    }
}
