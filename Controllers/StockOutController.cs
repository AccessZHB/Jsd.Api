using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockOut;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 出库管理接口
/// 路由前缀：/api/stockOut
/// 权限标识见 <see cref="StockOutPermissions"/>（按钮级：stockOut:list / detail / create / update / delete）
/// </summary>
[ApiController]
[Route("api/stockOut")]
[Authorize]  // 必须登录
public class StockOutController : ControllerBase
{
    private readonly IStockOutService _stockOutService;

    public StockOutController(IStockOutService stockOutService)
    {
        _stockOutService = stockOutService;
    }

    /// <summary>
    /// 1. 出库单分页列表（权限：stockOut:list）
    /// 示例：GET /api/stockOut/list?page=1&pageSize=10&keyword=CK2026&outType=1&status=1&startTime=2026-09-01&endTime=2026-09-12
    /// </summary>
    /// <param name="keyword">出库单号关键词（模糊，可空）</param>
    /// <param name="outType">出库类型：1-样品领用 2-报损 3-其他（可空=全部）</param>
    /// <param name="status">状态：0-待出库 1-已完成（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空，只传日期自动按当天结束处理）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockOutListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockOutListDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int? outType,
        [FromQuery] int? status,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockOutService.GetPagedListAsync(
            keyword, outType, status, startTime, endTime, page, pageSize);
    }

    /// <summary>
    /// 2. 出库单详情（主表 + 明细列表）（权限：stockOut:detail）
    /// 示例：GET /api/stockOut/detail/1
    /// </summary>
    /// <param name="id">出库单ID</param>
    [HttpGet("detail/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StockOutDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StockOutDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<StockOutDetailDto>> GetDetail(long id)
    {
        return await _stockOutService.GetDetailAsync(id);
    }

    /// <summary>
    /// 3. 创建出库单（写主表+明细，乐观锁扣减 SKU 库存，写库存变动日志；创建即完成）（权限：stockOut:create）
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<object>> Create([FromBody] StockOutCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空/范围校验，不通过直接返回 400；
        // 商品/SKU 不存在、库存不足、并发冲突等业务异常在此统一转换为业务错误响应
        try
        {
            return await _stockOutService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 4. 编辑出库单（旧明细回补库存 → 删旧明细 → 写新明细并扣减库存，事务保证一致）（权限：stockOut:update）
    /// 示例：PUT /api/stockOut/update/1
    /// </summary>
    /// <param name="id">出库单ID</param>
    [HttpPut("update/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] StockOutUpdateDto dto)
    {
        try
        {
            return await _stockOutService.UpdateAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 5. 删除出库单（按明细回补 SKU 库存，再删除明细与主表，物理删除）（权限：stockOut:delete）
    /// 示例：DELETE /api/stockOut/delete/1
    /// </summary>
    /// <param name="id">出库单ID</param>
    [HttpDelete("delete/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _stockOutService.DeleteAsync(id);
    }
}
