using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 商品（SPU）管理接口
/// 新增/修改为聚合接口：SPU + 规格 + SKU + 图片 一次性提交（后端事务保存）
/// </summary>
[ApiController]
[Route("api/prod-info")]
[Authorize]  // 必须登录
public class ProdInfoController : ControllerBase
{
    private readonly IProdInfoService _productService;

    public ProdInfoController(IProdInfoService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// 分页查询商品列表
    /// 示例：GET /api/prod-info/page?page=1&pageSize=10&keyword=镜片&categoryId=3&supplierId=1&status=1
    /// keyword 同时匹配商品名称和商品编码；返回值含 SKU 价格区间与库存总量
    /// </summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<ProdInfoDto>>> GetPage(
        [FromQuery] string? keyword,
        [FromQuery] long? categoryId,
        [FromQuery] long? supplierId,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _productService.GetPagedListAsync(keyword, categoryId, supplierId, status, page, pageSize);
    }

    /// <summary>
    /// 商品详情：SPU + 图片 + 全部规格(含规格值) + 全部 SKU
    /// 示例：GET /api/prod-info/123/detail
    /// </summary>
    [HttpGet("{id:long}/detail")]
    public async Task<ApiResponse<ProdInfoDetailDto>> GetDetail(long id)
    {
        return await _productService.GetDetailAsync(id);
    }

    /// <summary>
    /// 新增商品（聚合保存）。
    /// 请求体：SPU 基础字段 + specItems(规格项/值) + skus(前端组合好的SKU) + images(图片)
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] ProdInfoCreateDto dto)
    {
        return await _productService.CreateAsync(dto);
    }

    /// <summary>
    /// 修改商品（聚合更新，规格/SKU/图片全量替换）
    /// 示例：PUT /api/prod-info/123
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] ProdInfoUpdateDto dto)
    {
        // 以路由 id 为准，防止请求体 id 不一致
        dto.Id = id;
        return await _productService.UpdateAsync(dto);
    }

    /// <summary>
    /// 快速上下架（列表 Switch 开关）
    /// 示例：PUT /api/prod-info/123/status  body: { "status": 1 }
    /// </summary>
    [HttpPut("{id:long}/status")]
    public async Task<ApiResponse<object>> SetStatus(long id, [FromBody] SetStatusDto dto)
    {
        return await _productService.SetStatusAsync(id, dto.Status);
    }

    /// <summary>
    /// 删除商品（级联清理 SKU/规格/图片）
    /// 示例：DELETE /api/prod-info/123
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _productService.DeleteAsync(id);
    }
}

/// <summary>上下架请求体</summary>
public class SetStatusDto
{
    /// <summary>目标状态：1-上架 0-下架</summary>
    public int Status { get; set; }
}
