using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 商品 SKU 接口（价格/库存/编码在此层）
/// 业务约定（2026-09 改造）：
///   1) sku_code 由后端自动生成（SKU + yyyyMMddHHmmss + 4位随机数），前端传值一律忽略；
///   2) spec_values 存"规格值ID组合"（如 "10,15"），后端从前端提交的规格数组中提取 spec_value_id 拼接。
/// </summary>
[ApiController]
[Route("api/prod/sku")]
[Authorize]  // 必须登录
public class ProdSkuController : ControllerBase
{
    private readonly IProdSkuService _skuService;

    public ProdSkuController(IProdSkuService skuService)
    {
        _skuService = skuService;
    }

    /// <summary>
    /// 根据商品ID获取该商品的所有SKU
    /// 示例：GET /api/prod/sku/list?prodInfoId=1
    /// </summary>
    /// <param name="prodInfoId">商品ID（必填）</param>
    [HttpGet("list")]
    public async Task<ApiResponse<List<ProdSkuDto>>> GetList([FromQuery] long prodInfoId)
    {
        if (prodInfoId < 1)
        {
            return ApiResponse<List<ProdSkuDto>>.Fail("prodInfoId 参数不合法");
        }
        return await _skuService.GetListByProductAsync(prodInfoId);
    }

    /// <summary>
    /// 根据 ID 获取SKU详情
    /// 示例：GET /api/prod/sku/5
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<ProdSkuDto>> GetById(long id)
    {
        return await _skuService.GetByIdAsync(id);
    }

    /// <summary>
    /// 新增单个SKU（sku_code 由后端自动生成，无需前端传）
    /// 请求体：{ "prodInfoId": 1, "specs": [{ "specId": 3, "specValueId": 10 }, { "specId": 4, "specValueId": 15 }],
    ///           "retailPrice": 199.00, "salePrice": 259.00, "stock": 100 }
    /// 说明：specs 与 specValues 二选一即可；specValues 可直接传 "10,15"。
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] ProdSkuCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空校验，不通过直接返回 400
        return await _skuService.CreateAsync(dto);
    }

    /// <summary>
    /// 修改SKU（价格、库存、规格组合；sku_code 不可修改）
    /// 请求体：{ "id": 5, "specs": [{ "specId": 3, "specValueId": 10 }], "retailPrice": 219.00, "stock": 80 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] ProdSkuUpdateDto dto)
    {
        return await _skuService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除SKU
    /// 示例：DELETE /api/prod/sku/5
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _skuService.DeleteAsync(id);
    }
}
