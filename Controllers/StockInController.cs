using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockIn;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 入库管理接口
/// 路由前缀：/api/stockIn
/// </summary>
[ApiController]
[Route("api/stockIn")]
[Authorize]  // 必须登录
public class StockInController : ControllerBase
{
    private readonly IStockInService _stockInService;

    public StockInController(IStockInService stockInService)
    {
        _stockInService = stockInService;
    }

    /// <summary>
    /// 1. 入库单分页列表
    /// 示例：GET /api/stockIn/list?page=1&pageSize=10&keyword=RK2026&supplierId=1&status=1&startTime=2026-09-01&endTime=2026-09-12
    /// </summary>
    /// <param name="keyword">入库单号关键词（模糊，可空）</param>
    /// <param name="supplierId">供应商ID（可空）</param>
    /// <param name="status">状态：0-待入库 1-已完成 2-已取消（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空，只传日期自动按当天结束处理）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockInListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockInListDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] long? supplierId,
        [FromQuery] int? status,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockInService.GetPagedListAsync(
            keyword, supplierId, status, startTime, endTime, page, pageSize);
    }

    /// <summary>
    /// 2. 入库单详情（主表 + 明细列表）
    /// 示例：GET /api/stockIn/detail/1
    /// </summary>
    /// <param name="id">入库单ID</param>
    [HttpGet("detail/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StockInDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StockInDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<StockInDetailDto>> GetDetail(long id)
    {
        return await _stockInService.GetDetailAsync(id);
    }

    /// <summary>
    /// 3. 创建入库单（写主表+明细，增加 SKU 库存，写库存变动日志；创建即完成）
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<object>> Create([FromBody] StockInCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空/范围校验，不通过直接返回 400；
        // 商品/SKU/供应商不存在等业务异常在此统一转换为业务错误响应
        try
        {
            return await _stockInService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 4. 编辑入库单（旧明细冲减库存 → 删旧明细 → 写新明细并增加库存，事务保证一致）
    /// 示例：PUT /api/stockIn/update/1
    /// </summary>
    /// <param name="id">入库单ID</param>
    [HttpPut("update/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] StockInUpdateDto dto)
    {
        try
        {
            return await _stockInService.UpdateAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 5. 删除入库单（按明细冲减 SKU 库存，再删除明细与主表，物理删除）
    /// 示例：DELETE /api/stockIn/delete/1
    /// </summary>
    /// <param name="id">入库单ID</param>
    [HttpDelete("delete/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _stockInService.DeleteAsync(id);
    }
}
