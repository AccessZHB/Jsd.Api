using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockCheck;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 库存盘点接口
/// 路由前缀：/api/stockCheck（与 stockIn / stockOut 一致采用 camelCase，无连字符）
/// 权限标识见 <see cref="StockCheckPermissions"/>（按钮级：stock_check:list / detail / add / edit / delete）
/// 无审核环节：创建即 盘点中，录入实盘即 已完成。
/// </summary>
[ApiController]
[Route("api/stockCheck")]
[Authorize]  // 必须登录
public class StockCheckController : ControllerBase
{
    private readonly IStockCheckService _stockCheckService;

    public StockCheckController(IStockCheckService stockCheckService)
    {
        _stockCheckService = stockCheckService;
    }

    /// <summary>
    /// 1. 分页查询盘点单（多条件筛选 + 差异汇总）（权限：stock_check:list）
    /// 示例：GET /api/stockCheck?page=1&pageSize=10&keyword=PD2026&status=0&startTime=2026-09-01&endTime=2026-09-12
    /// </summary>
    /// <param name="keyword">盘点单号关键词（模糊，可空）</param>
    /// <param name="status">状态：0-盘点中 1-已完成（可空=全部）</param>
    /// <param name="startTime">创建时间起（可空）</param>
    /// <param name="endTime">创建时间止（可空，只传日期自动按当天结束处理）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockCheckListDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockCheckListDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int? status,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockCheckService.GetPagedListAsync(
            keyword, status, startTime, endTime, page, pageSize);
    }

    /// <summary>
    /// 2. 获取盘点单详情（含明细列表）（权限：stock_check:detail）
    /// 示例：GET /api/stockCheck/1
    /// </summary>
    /// <param name="id">盘点单ID</param>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StockCheckDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StockCheckDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<StockCheckDetailDto>> GetDetail(long id)
    {
        return await _stockCheckService.GetDetailAsync(id);
    }

    /// <summary>
    /// 3. 创建盘点单（生成单号 + 带入系统库存快照 + 写变动日志，状态=盘点中）（权限：stock_check:add）
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<object>> Create([FromBody] StockCheckCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空/范围校验；商品/SKU 不存在等业务异常在此统一转换
        try
        {
            return await _stockCheckService.CreateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 4. 完成盘点（录入实盘数量 + 计算盈亏 + 调整库存 + 写变动日志，状态=已完成）（权限：stock_check:edit）
    /// 示例：PUT /api/stockCheck/1
    /// 仅 盘点中(0) 单据可调用；已完成单据不可重复操作。
    /// </summary>
    /// <param name="id">盘点单ID</param>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Complete(long id, [FromBody] StockCheckUpdateDto dto)
    {
        try
        {
            return await _stockCheckService.CompleteAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 5. 删除盘点单（仅 盘点中(0) 可删）（权限：stock_check:delete）
    /// 示例：DELETE /api/stockCheck/1
    /// </summary>
    /// <param name="id">盘点单ID</param>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _stockCheckService.DeleteAsync(id);
    }
}
