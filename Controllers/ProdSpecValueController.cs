using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 商品规格值接口（规格项下的可选值：红色/M/1.56…）
/// </summary>
[ApiController]
[Route("api/prod/spec-value")]
[Authorize]  // 必须登录
public class ProdSpecValueController : ControllerBase
{
    private readonly IProdSpecService _specService;

    public ProdSpecValueController(IProdSpecService specService)
    {
        _specService = specService;
    }

    /// <summary>
    /// 根据规格项ID获取该规格项的所有规格值
    /// 示例：GET /api/prod/spec-value/list?specId=1
    /// </summary>
    /// <param name="specId">规格项ID（必填）</param>
    [HttpGet("list")]
    public async Task<ApiResponse<List<SpecValueDto>>> GetList([FromQuery] long specId)
    {
        if (specId < 1)
        {
            return ApiResponse<List<SpecValueDto>>.Fail("specId 参数不合法");
        }
        return await _specService.GetValuesByItemAsync(specId);
    }

    /// <summary>
    /// 新增规格值
    /// 请求体：{ "specId": 1, "specValue": "红色", "sort": 1 }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] SpecValueCreateDto dto)
    {
        // [ApiController] + DTO 特性自动做非空校验，不通过直接返回 400
        return await _specService.CreateValueAsync(dto);
    }

    /// <summary>
    /// 修改规格值
    /// 请求体：{ "id": 1, "specValue": "深红色", "sort": 1 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] SpecValueUpdateDto dto)
    {
        return await _specService.UpdateValueAsync(dto);
    }

    /// <summary>
    /// 删除规格值
    /// 示例：DELETE /api/prod/spec-value/1
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _specService.DeleteValueAsync(id);
    }
}
