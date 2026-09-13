using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockWarning;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 库存预警接口
/// 路由前缀：/api/stockWarning（与 stockIn / stockOut / stockCheck 一致采用 camelCase，无连字符）
/// 权限标识见 <see cref="StockWarningPermissions"/>（按钮级：stock_warning:list / edit / enable）
///
/// 接口清单：
///   1. GET    /api/stockWarning              分页查询（商品/状态/仅看预警 筛选 + 当前库存 + 是否触发预警）
///   2. GET    /api/stockWarning/{id}         单条详情（编辑回显，配合"编辑"按钮使用）
///   3. POST   /api/stockWarning              新增预警配置
///   4. PUT    /api/stockWarning              编辑预警配置（id 放在请求体）
///   5. PUT    /api/stockWarning/enable/{id}  启用/停用切换
///   6. DELETE /api/stockWarning/{id}         删除预警配置
///
/// 说明：本模块权限只有 list / edit / enable 三个，没有独立的删除权限，
///       删除复用 stock_warning:edit（由前端 v-permission 控制按钮显隐）。
/// </summary>
[ApiController]
[Route("api/stockWarning")]
[Authorize]  // 必须登录
public class StockWarningController : ControllerBase
{
    private readonly IStockWarningService _stockWarningService;

    public StockWarningController(IStockWarningService stockWarningService)
    {
        _stockWarningService = stockWarningService;
    }

    /// <summary>
    /// 1. 分页查询预警配置列表（权限：stock_warning:list）
    /// 多条件筛选 + 关联商品/SKU名称 + 当前库存 + 是否已触发预警。
    /// 示例：GET /api/stockWarning?prodInfoId=1&amp;status=1&amp;onlyWarning=true&amp;page=1&amp;pageSize=10
    /// </summary>
    /// <param name="prodInfoId">商品ID筛选（可空）</param>
    /// <param name="status">状态：1-启用 0-停用（可空=全部）</param>
    /// <param name="onlyWarning">true=仅返回已触发预警的配置（可空）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockWarningItemDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<PagedResult<StockWarningItemDto>>> GetList(
        [FromQuery] long? prodInfoId,
        [FromQuery] int? status,
        [FromQuery] bool? onlyWarning,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _stockWarningService.GetPagedListAsync(
            prodInfoId, status, onlyWarning, page, pageSize);
    }

    /// <summary>
    /// 2. 获取单条预警配置（编辑回显用）（权限：stock_warning:edit）
    /// 示例：GET /api/stockWarning/1
    /// </summary>
    /// <param name="id">预警配置ID</param>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StockWarningItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StockWarningItemDto>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<StockWarningItemDto>> GetDetail(long id)
    {
        return await _stockWarningService.GetDetailAsync(id);
    }

    /// <summary>
    /// 3. 新增预警配置（权限：stock_warning:edit）
    /// 含参数校验：商品存在性、SKU 归属、阈值非负、同商品同SKU不重复。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<object>> Create([FromBody] StockWarningSaveDto dto)
    {
        // [ApiController] + DTO 特性自动做声明式校验；业务校验（商品/SKU 不存在、重复配置等）
        // 由 Service 抛 InvalidOperationException，在此统一转 400
        try
        {
            return await _stockWarningService.SaveAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 4. 编辑预警配置（权限：stock_warning:edit）
    /// id 放在请求体中（与 spec 的 POST/PUT /api/stock-warning 一致）。
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Update([FromBody] StockWarningSaveDto dto)
    {
        try
        {
            return await _stockWarningService.SaveAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 5. 启用 / 停用切换（权限：stock_warning:enable）
    /// 请求体 status 传值则设为指定状态；不传则按当前状态取反。
    /// 示例：PUT /api/stockWarning/enable/1  body: { "status": 0 }
    /// </summary>
    /// <param name="id">预警配置ID</param>
    [HttpPut("enable/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> SetEnable(long id, [FromBody] StockWarningEnableDto? dto)
    {
        return await _stockWarningService.SetEnableAsync(id, dto ?? new StockWarningEnableDto());
    }

    /// <summary>
    /// 6. 删除预警配置（复用 stock_warning:edit 权限）
    /// 本表为纯配置表，删除不牵动库存、无业务流水，直接物理删除。
    /// 示例：DELETE /api/stockWarning/1
    /// </summary>
    /// <param name="id">预警配置ID</param>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _stockWarningService.DeleteAsync(id);
    }
}
