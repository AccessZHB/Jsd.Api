using Jsd.Api.Models.Common;
using Jsd.Api.Models.Supplier;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 供应商管理接口（基础数据模块）
/// </summary>
[ApiController]
[Route("api/sup/supplier")]
[Authorize]  // 必须登录
public class SupSupplierController : ControllerBase
{
    private readonly ISupSupplierService _supplierService;

    public SupSupplierController(ISupSupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>
    /// 分页查询供应商列表
    /// 示例：GET /api/sup/supplier/list?page=1&pageSize=10&keyword=眼睛&status=1
    /// </summary>
    /// <param name="keyword">供应商名称关键词（模糊搜索，可空）</param>
    /// <param name="status">状态筛选（可空：不传=全部，1=启用，0=禁用）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _supplierService.GetPagedListAsync(keyword, status, page, pageSize);
    }

    /// <summary>
    /// 获取所有启用状态的供应商（商品录入时的下拉选择，不分页）
    /// 示例：GET /api/sup/supplier/all
    /// </summary>
    [HttpGet("all")]
    public async Task<ApiResponse<List<SupplierDto>>> GetAll()
    {
        return await _supplierService.GetAllEnabledAsync();
    }

    /// <summary>
    /// 根据 ID 获取供应商详情（用于修改表单回显）
    /// 示例：GET /api/sup/supplier/1
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<SupplierDto>> GetById(long id)
    {
        return await _supplierService.GetByIdAsync(id);
    }

    /// <summary>
    /// 新增供应商
    /// 请求体：{ "supplierName": "明月镜片", "contactPerson": "张三", "contactPhone": "13800138000" }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] SupplierCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空校验，不通过直接返回 400
        return await _supplierService.CreateAsync(dto);
    }

    /// <summary>
    /// 修改供应商信息
    /// 请求体：{ "id": 1, "supplierName": "明月镜片", "contactPerson": "张三", "status": 1 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] SupplierUpdateDto dto)
    {
        return await _supplierService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除供应商（存在关联商品时拒绝，提示改为禁用）
    /// 示例：DELETE /api/sup/supplier/1
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _supplierService.DeleteAsync(id);
    }
}
