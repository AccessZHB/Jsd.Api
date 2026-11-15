using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 字典数据接口（字典管理模块 B 组，共 6 个）
///
/// 其中 GET /api/dict/data/type/{dictType} 是核心缓存接口：命中缓存直接返回，
/// 使前端下拉框不查库；任何增删改都会自动失效对应缓存。
///
/// 操作日志：写操作打 [OperationLog]，由 OperationLogFilter 异步采集落库。
/// </summary>
[ApiController]
[Route("api/dict/data")]
[Authorize]
public class DictDataController : ControllerBase
{
    private readonly IDictDataService _dictDataService;

    public DictDataController(IDictDataService dictDataService)
    {
        _dictDataService = dictDataService;
    }

    /// <summary>字典数据列表（分页 + 按类型编码/标签搜索）</summary>
    /// <remarks>
    /// GET /api/dict/data/list?dictTypeId=&amp;dictTypeCode=&amp;dictLabel=&amp;status=0&amp;page=1&amp;pageSize=10
    /// </remarks>
    [HttpGet("list")]
    [Permission(DictPermissions.DataList)]     // dict:data:list
    public async Task<ApiResponse<PagedResult<DictDataDto>>> GetList([FromQuery] DictDataQueryDto query)
        => await _dictDataService.GetPagedAsync(query);

    /// <summary>字典数据详情</summary>
    /// <remarks>GET /api/dict/data/1</remarks>
    [HttpGet("{id:long}")]
    [Permission(DictPermissions.DataList)]     // 详情复用 list
    public async Task<ApiResponse<DictDataDto?>> GetById(long id)
        => await _dictDataService.GetByIdAsync(id);

    /// <summary>按字典类型编码获取所有启用数据（核心缓存接口）</summary>
    /// <remarks>
    /// GET /api/dict/data/type/sys_order_status
    /// 不挂权限：全站下拉框（订单状态/支付方式/售后原因…）都依赖它，
    /// 挂权限会导致无字典权限的角色页面下拉框全空。
    /// </remarks>
    [HttpGet("type/{dictType}")]
    public async Task<ApiResponse<List<DictDataItem>>> GetByDictType(string dictType)
        => await _dictDataService.GetByDictTypeAsync(dictType);

    /// <summary>新增字典数据</summary>
    /// <remarks>POST /api/dict/data</remarks>
    [HttpPost]
    [Permission(DictPermissions.DataCreate)]   // dict:data:create
    [OperationLog("字典管理", "新增字典数据")]
    public async Task<ApiResponse<long>> Create([FromBody] CreateDictDataDto dto)
    {
        try
        {
            return await _dictDataService.CreateAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>修改字典数据</summary>
    /// <remarks>PUT /api/dict/data</remarks>
    [HttpPut]
    [Permission(DictPermissions.DataUpdate)]   // dict:data:update
    [OperationLog("字典管理", "修改字典数据")]
    public async Task<ApiResponse<bool>> Update([FromBody] UpdateDictDataDto dto)
    {
        try
        {
            return await _dictDataService.UpdateAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>启用/停用字典数据</summary>
    /// <remarks>PUT /api/dict/data/{id}/status   body: { "status": 0 }</remarks>
    [HttpPut("{id:long}/status")]
    [Permission(DictPermissions.DataUpdate)]   // 启停复用 update
    [OperationLog("字典管理", "启用停用字典数据")]
    public async Task<ApiResponse<bool>> ChangeStatus(long id, [FromBody] DictStatusDto dto)
        => await _dictDataService.ChangeStatusAsync(id, dto);

    /// <summary>删除字典数据</summary>
    /// <remarks>DELETE /api/dict/data/1</remarks>
    [HttpDelete("{id:long}")]
    [Permission(DictPermissions.DataDelete)]   // dict:data:delete
    [OperationLog("字典管理", "删除字典数据")]
    public async Task<ApiResponse<bool>> Delete(long id)
        => await _dictDataService.DeleteAsync(id);
}
