using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 商品规格项接口（属性维度：颜色/尺码/折射率/膜层…）
/// </summary>
[ApiController]
[Route("api/prod/spec-item")]
[Authorize]  // 必须登录
public class ProdSpecItemController : ControllerBase
{
    private readonly IProdSpecService _specService;

    public ProdSpecItemController(IProdSpecService specService)
    {
        _specService = specService;
    }

    /// <summary>
    /// 根据商品ID获取该商品的所有规格项
    /// 示例：GET /api/prod/spec-item/list?prodInfoId=1
    /// </summary>
    /// <param name="prodInfoId">商品ID（必填）</param>
    [HttpGet("list")]
    public async Task<ApiResponse<List<SpecItemDto>>> GetList([FromQuery] long prodInfoId)
    {
        if (prodInfoId < 1)
        {
            return ApiResponse<List<SpecItemDto>>.Fail("prodInfoId 参数不合法");
        }
        return await _specService.GetItemsByProductAsync(prodInfoId);
    }

    /// <summary>
    /// 新增规格项
    /// 请求体：{ "prodInfoId": 1, "specName": "颜色", "sort": 1 }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] SpecItemCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空校验，不通过直接返回 400
        return await _specService.CreateItemAsync(dto);
    }

    /// <summary>
    /// 修改规格项（改名/排序）
    /// 请求体：{ "id": 1, "specName": "镜片颜色", "sort": 1 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] SpecItemUpdateDto dto)
    {
        return await _specService.UpdateItemAsync(dto);
    }

    /// <summary>
    /// 删除规格项（级联删除其下所有规格值）
    /// 示例：DELETE /api/prod/spec-item/1
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _specService.DeleteItemAsync(id);
    }
}
