using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysMenu;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 系统菜单接口（左侧导航栏 / 动态路由 / 菜单管理页）
/// </summary>
[ApiController]
[Route("api/sys/menu")]
[Authorize]  // 必须登录（携带有效 JWT）才能访问
public class SysMenuController : ControllerBase
{
    private readonly ISysMenuService _menuService;

    public SysMenuController(ISysMenuService menuService)
    {
        _menuService = menuService;
    }

    /// <summary>
    /// 获取当前登录用户的菜单树
    /// （根据 Token 中的用户ID → 角色 → sys_role_menu 授权 → 组装菜单树）
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<List<MenuTreeNodeDto>>> GetMenuTree()
    {
        return await _menuService.GetCurrentUserMenuTreeAsync();
    }

    /// <summary>
    /// 根据 ID 获取菜单详情（用于修改表单回显）
    /// 示例：GET /api/sys/menu/100
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<SysMenuDto>> GetById(long id)
    {
        return await _menuService.GetByIdAsync(id);
    }

    /// <summary>
    /// 修改菜单
    /// 请求体：{ "id": 100, "parentId": 1, "menuName": "管理员管理", "path": "user", "menuType": 2 }
    /// </summary>
    [HttpPut]
    public async Task<ApiResponse<object>> Update([FromBody] SysMenuUpdateDto dto)
    {
        // [ApiController] + DTO 上的 [Required]/[Range] 特性自动做参数校验，不通过直接返回 400
        return await _menuService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除菜单（单个；存在子菜单时会被拒绝）
    /// 示例：DELETE /api/sys/menu/10000
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _menuService.DeleteAsync(id);
    }

    /// <summary>
    /// 批量删除菜单（可整棵子树一起删，但不允许留下"孤儿"子菜单）
    /// 示例：DELETE /api/sys/menu/batch?ids=10000,10001
    /// </summary>
    [HttpDelete("batch")]
    public async Task<ApiResponse<object>> DeleteBatch([FromQuery] string ids)
    {
        var idList = ParseIds(ids);
        if (idList.Length == 0)
        {
            return ApiResponse<object>.Fail("ids 参数格式错误，示例：ids=10000,10001");
        }
        return await _menuService.DeleteBatchAsync(idList);
    }

    /// <summary>
    /// 把 "10000,10001" 格式的字符串解析成 long 数组（批量删除用）。
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
