using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 商品分类管理接口（支持多级树形结构）
/// </summary>
[ApiController]
[Route("api/prod/category")]
[Authorize]  // 必须登录
public class ProdCategoryController : ControllerBase
{
    private readonly IProdCategoryService _categoryService;

    public ProdCategoryController(IProdCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// 获取分类树形结构（递归组装父子关系）
    /// 示例：GET /api/prod/category/tree
    /// </summary>
    [HttpGet("tree")]
    public async Task<ApiResponse<List<CategoryTreeNodeDto>>> GetTree()
    {
        return await _categoryService.GetTreeAsync();
    }

    /// <summary>
    /// 分页查询分类列表（扁平结构，支持按名称搜索）
    /// 示例：GET /api/prod/category/list?page=1&pageSize=10&keyword=镜片
    /// </summary>
    /// <param name="keyword">分类名称关键词（可空）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<CategoryDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _categoryService.GetPagedListAsync(keyword, page, pageSize);
    }

    /// <summary>
    /// 根据 ID 获取分类详情（用于修改表单回显）
    /// 示例：GET /api/prod/category/3
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<CategoryDto>> GetById(long id)
    {
        return await _categoryService.GetByIdAsync(id);
    }

    /// <summary>
    /// 新增分类
    /// 请求体：{ "categoryName": "太阳镜", "parentId": 1, "sort": 1 }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] CategoryCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空校验，不通过直接返回 400
        return await _categoryService.CreateAsync(dto);
    }

    /// <summary>
    /// 修改分类
    /// 请求体：{ "id": 3, "categoryName": "太阳镜", "parentId": 1, "sort": 2, "status": 1 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] CategoryUpdateDto dto)
    {
        return await _categoryService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除分类（有子分类或关联商品时拒绝删除）
    /// 示例：DELETE /api/prod/category/3
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _categoryService.DeleteAsync(id);
    }
}
