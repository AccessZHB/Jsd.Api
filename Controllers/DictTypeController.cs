using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 字典类型接口（字典管理模块 A 组，共 6 个）
///
/// 权限标识见 <see cref="DictPermissions"/>：
/// dict:type:list / create / update / delete（按钮级，由 sys_menu.permission 下发）
///
/// 操作日志：写操作打 [OperationLog]，由 OperationLogFilter 异步采集落库，Controller 内不写日志代码。
/// </summary>
[ApiController]
[Route("api/dict/type")]
[Authorize]
public class DictTypeController : ControllerBase
{
    private readonly IDictTypeService _dictTypeService;

    public DictTypeController(IDictTypeService dictTypeService)
    {
        _dictTypeService = dictTypeService;
    }

    /// <summary>字典类型列表（分页 + 条件搜索）</summary>
    /// <remarks>GET /api/dict/type/list?dictName=&amp;dictType=&amp;status=0&amp;page=1&amp;pageSize=10</remarks>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<DictTypeDto>>> GetList([FromQuery] DictTypeQueryDto query)
        => await _dictTypeService.GetPagedAsync(query);

    /// <summary>字典类型下拉选项（仅返回启用状态的类型）</summary>
    /// <remarks>GET /api/dict/type/options</remarks>
    [HttpGet("options")]
    public async Task<ApiResponse<List<DictTypeOptionDto>>> GetOptions()
        => await _dictTypeService.GetOptionsAsync();

    /// <summary>字典类型详情</summary>
    /// <remarks>GET /api/dict/type/1</remarks>
    [HttpGet("{id:long}")]
    public async Task<ApiResponse<DictTypeDto?>> GetById(long id)
        => await _dictTypeService.GetByIdAsync(id);

    /// <summary>新增字典类型</summary>
    /// <remarks>POST /api/dict/type</remarks>
    [HttpPost]
    [OperationLog("字典管理", "新增字典类型")]
    public async Task<ApiResponse<long>> Create([FromBody] CreateDictTypeDto dto)
    {
        try
        {
            return await _dictTypeService.CreateAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<long>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>修改字典类型</summary>
    /// <remarks>PUT /api/dict/type</remarks>
    [HttpPut]
    [OperationLog("字典管理", "修改字典类型")]
    public async Task<ApiResponse<bool>> Update([FromBody] UpdateDictTypeDto dto)
    {
        try
        {
            return await _dictTypeService.UpdateAsync(dto);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
    }

    /// <summary>启用/停用字典类型</summary>
    /// <remarks>PUT /api/dict/type/{id}/status   body: { "status": 0 }</remarks>
    [HttpPut("{id:long}/status")]
    [OperationLog("字典管理", "启用停用字典类型")]
    public async Task<ApiResponse<bool>> ChangeStatus(long id, [FromBody] DictStatusDto dto)
        => await _dictTypeService.ChangeStatusAsync(id, dto);

    /// <summary>删除字典类型（同一事务内级联删除其下字典数据）</summary>
    /// <remarks>DELETE /api/dict/type/1</remarks>
    [HttpDelete("{id:long}")]
    [OperationLog("字典管理", "删除字典类型")]
    public async Task<ApiResponse<bool>> Delete(long id)
        => await _dictTypeService.DeleteAsync(id);
}
