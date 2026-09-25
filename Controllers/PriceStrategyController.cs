using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 价格策略接口（价格策略与会员余额模块）
/// 路由前缀：/api/price-strategies
///
/// 【状态流转】0-草稿 ──提交──&gt; 1-启用 ──停用──&gt; 2-停用
///   仅草稿可修改基本信息与维护规则；提交启用时会校验规则完整性。
/// 【规则维护】/rules/batch 为全量覆盖（未提交的旧规则自动停用），事务内执行并记录变更日志。
/// </summary>
[ApiController]
[Route("api/price-strategies")]
[Authorize]
public class PriceStrategyController : ControllerBase
{
    private readonly IPriceService _priceService;

    public PriceStrategyController(IPriceService priceService)
    {
        _priceService = priceService;
    }

    /// <summary>分页查询价格策略（类型 / 状态 / 关键字 / 日期范围）</summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PriceStrategyDto>>> GetPage([FromQuery] PriceStrategyQueryDto query)
    {
        return await _priceService.GetStrategiesAsync(query);
    }

    /// <summary>新增价格策略（默认草稿，自动生成 strategy_no）</summary>
    [HttpPost]
    public async Task<ApiResponse<long>> Create([FromBody] CreatePriceStrategyDto dto)
    {
        try
        {
            return await _priceService.CreateStrategyAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>更新策略基本信息（仅草稿可改）</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResponse<bool>> Update(long id, [FromBody] UpdatePriceStrategyDto dto)
    {
        try
        {
            return await _priceService.UpdateStrategyAsync(id, dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>草稿提交启用（校验规则完整性）</summary>
    [HttpPatch("{id:long}/submit")]
    public async Task<ApiResponse<bool>> Submit(long id)
    {
        return await _priceService.SubmitStrategyAsync(id);
    }

    /// <summary>停用策略（记录变更日志）</summary>
    [HttpPatch("{id:long}/disable")]
    public async Task<ApiResponse<bool>> Disable(long id, [FromBody] DisableStrategyDto? dto)
    {
        return await _priceService.DisableStrategyAsync(id, dto?.Reason);
    }

    /// <summary>删除策略（仅草稿）</summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResponse<bool>> Delete(long id)
    {
        return await _priceService.DeleteStrategyAsync(id);
    }

    /// <summary>查询策略下的规则及明细</summary>
    [HttpGet("{id:long}/rules")]
    public async Task<ApiResponse<List<PriceRuleDto>>> GetRules(long id)
    {
        return await _priceService.GetRulesAsync(id);
    }

    /// <summary>批量维护规则及明细（全量覆盖，事务内执行）</summary>
    [HttpPost("{id:long}/rules/batch")]
    public async Task<ApiResponse<bool>> SaveRulesBatch(long id, [FromBody] PriceRuleBatchSaveDto dto)
    {
        try
        {
            return await _priceService.SaveRulesBatchAsync(id, dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }
}
