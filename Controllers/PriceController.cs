using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 取价与价格变更日志接口（价格策略与会员余额模块）
///
/// POST /api/price/quotation —— 核心取价接口：
///   按优先级 客户专属价 &gt; 促销价 &gt; 客户等级价 &gt; 批量阶梯价 &gt; 商品标准售价 严格匹配，
///   输出每个商品的最终价、命中策略类型、折扣信息、是否触发底价保护（不拦截，仅打标）。
///
/// GET /api/price-change-logs —— 价格变更审计日志。
/// </summary>
[ApiController]
[Authorize]
public class PriceController : ControllerBase
{
    private readonly IPriceService _priceService;

    public PriceController(IPriceService priceService)
    {
        _priceService = priceService;
    }

    /// <summary>
    /// 核心取价：入参会员ID + 商品清单（物料ID + 数量），返回逐行最终成交价与命中详情。
    /// </summary>
    [HttpPost("api/price/quotation")]
    public async Task<ApiResponse<List<QuotationResultDto>>> Quotation([FromBody] QuotationRequestDto dto)
    {
        try
        {
            return await _priceService.QuotationAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<List<QuotationResultDto>>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<List<QuotationResultDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 商品物料（SKU）下拉搜索：价格规则「指定商品 / 阶梯价」明细行选择商品时使用。
    /// keyword 为空时返回最近 50 个启用 SKU。
    /// </summary>
    [HttpGet("api/price/materials")]
    public async Task<ApiResponse<List<MaterialOptionDto>>> GetMaterials(
        [FromQuery] string? keyword, [FromQuery] int limit = 50)
    {
        return await _priceService.GetMaterialsAsync(keyword, limit);
    }

    /// <summary>分页查询价格变更日志（策略/规则/操作类型/时间范围筛选）</summary>
    [HttpGet("api/price-change-logs")]
    public async Task<ApiResponse<PagedResult<PriceChangeLogDto>>> GetChangeLogs([FromQuery] PriceChangeLogQueryDto query)
    {
        return await _priceService.GetChangeLogsAsync(query);
    }
}
