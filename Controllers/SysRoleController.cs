using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysRole;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 系统角色管理接口
/// </summary>
[ApiController]
[Route("api/sys/role")]
[Authorize]  // 必须登录
public class SysRoleController : ControllerBase
{
    private readonly ISysRoleService _roleService;

    public SysRoleController(ISysRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>
    /// 获取角色列表
    /// 示例：GET /api/sys/role/list
    /// </summary>
    [HttpGet("list")]
    public async Task<ApiResponse<List<SysRoleDto>>> GetList()
    {
        return await _roleService.GetListAsync();
    }

    /// <summary>
    /// 根据 ID 获取角色详情（用于修改表单回显）
    /// 示例：GET /api/sys/role/2
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<SysRoleDto>> GetById(long id)
    {
        return await _roleService.GetByIdAsync(id);
    }

    /// <summary>
    /// 修改角色
    /// 请求体：{ "id": 2, "roleName": "运营专员", "roleCode": "operation", "dataScope": 1, "status": 1 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] SysRoleUpdateDto dto)
    {
        // [ApiController] + DTO 上的 [Required]/[Range] 特性自动做参数校验，不通过直接返回 400
        return await _roleService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除角色（单个；内置 admin 角色与已绑定用户的角色会被拒绝）
    /// 示例：DELETE /api/sys/role/4
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _roleService.DeleteAsync(id);
    }

    /// <summary>
    /// 批量删除角色
    /// 示例：DELETE /api/sys/role/batch?ids=3,4
    /// </summary>
    [HttpDelete("batch")]
    public async Task<ApiResponse<object>> DeleteBatch([FromQuery] string ids)
    {
        var idList = ParseIds(ids);
        if (idList.Length == 0)
        {
            return ApiResponse<object>.Fail("ids 参数格式错误，示例：ids=3,4");
        }
        return await _roleService.DeleteBatchAsync(idList);
    }

    /// <summary>
    /// 给角色分配菜单权限（全量替换模式）
    /// 请求体：{ "roleId": 3, "menuIds": [3, 300, 30000] }
    /// 说明：menuIds 传最终勾选结果（含目录/菜单/按钮的 Id）；传空数组 = 清空该角色全部权限
    /// </summary>
    [HttpPut("auth")]
    public async Task<ApiResponse<object>> AssignMenus([FromBody] SysRoleAuthDto dto)
    {
        return await _roleService.AssignMenusAsync(dto);
    }

    /// <summary>
    /// 获取角色已授权的菜单ID集合（用于分配菜单弹窗回显勾选状态）
    /// 示例：GET /api/sys/role/3/menus
    /// 返回：{ "code": 200, "data": [1, 100, 101, 102] }
    /// </summary>
    [HttpGet("{id}/menus")]
    public async Task<ApiResponse<List<long>>> GetRoleMenuIds(long id)
    {
        return await _roleService.GetRoleMenuIdsAsync(id);
    }

    /// <summary>
    /// 把 "3,4" 格式的字符串解析成 long 数组（批量删除用）。
    /// 自动去除空格/空项，忽略非法数字并去重。
    /// </summary>
    private static long[] ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Array.Empty<long>();
        }

        return ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}
