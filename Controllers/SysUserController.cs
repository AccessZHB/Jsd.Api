using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysUser;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 系统用户（管理员）管理接口
/// </summary>
[ApiController]
[Route("api/sys/user")]
[Authorize]  // 必须登录
public class SysUserController : ControllerBase
{
    private readonly ISysUserService _userService;

    public SysUserController(ISysUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// 分页查询用户列表（含角色名称）
    /// 示例：GET /api/sys/user/list?page=1&pageSize=10&keyword=admin
    /// </summary>
    /// <param name="keyword">搜索关键词（账号/姓名/手机号，可空）</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页条数，默认10</param>
    [HttpGet("list")]
    public async Task<ApiResponse<PagedResult<SysUserListDto>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return await _userService.GetPagedListAsync(keyword, page, pageSize);
    }

    /// <summary>
    /// 根据 ID 获取用户详情（用于修改表单回显）
    /// 示例：GET /api/sys/user/1
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<SysUserListDto>> GetById(long id)
    {
        return await _userService.GetByIdAsync(id);
    }

    /// <summary>
    /// 新增用户
    /// 请求体：{ "userName": "zhangsan", "password": "123456", "realName": "张三", "roleId": 2 }
    /// </summary>
    [HttpPost]
    [Permission(SysUserPermissions.Add)]   // system:user:add —— 后端硬校验，未授权角色返回 403
    public async Task<ApiResponse<object>> Create([FromBody] SysUserCreateDto dto)
    {
        return await _userService.CreateAsync(dto);
    }

    /// <summary>
    /// 修改用户（不支持修改密码）
    /// 请求体：{ "id": 2, "userName": "zhangsan", "realName": "张三", "roleId": 3, "status": 1 }
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<object>> Update(int id, [FromBody] SysUserUpdateDto dto)
    {
        dto.Id = id;
        // [ApiController] + DTO 上的 [Required]/[Range] 特性会自动做参数校验，
        // 校验不通过时框架直接返回 400，不会进入本方法，无需手写 if 判空
        return await _userService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除用户（单个）
    /// 示例：DELETE /api/sys/user/2
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _userService.DeleteAsync(id);
    }

    /// <summary>
    /// 重置用户密码（管理员后台，不需要旧密码）
    /// 请求体：{ "password": "123456" }
    /// 示例：PUT /api/sys/user/1/password
    /// 服务端硬护栏（见 SysUserService.ResetPasswordAsync）：
    ///   - 禁止重置当前登录账号自身；
    ///   - 非超级管理员禁止重置超级管理员账号；
    ///   - 非超级管理员须拥有 system:user:edit 权限。
    /// </summary>
    [HttpPut("{id}/password")]
    [OperationLog("系统管理", "重置用户密码")]
    public async Task<ApiResponse<object>> ResetPassword(long id, [FromBody] ResetPasswordDto dto)
    {
        return await _userService.ResetPasswordAsync(id, dto);
    }

    /// <summary>
    /// 批量删除用户
    /// 示例：DELETE /api/sys/user/batch?ids=2,3,4
    /// </summary>
    [HttpDelete("batch")]
    public async Task<ApiResponse<object>> DeleteBatch([FromQuery] string ids)
    {
        var idList = ParseIds(ids);
        if (idList.Length == 0)
        {
            return ApiResponse<object>.Fail("ids 参数格式错误，示例：ids=2,3,4");
        }
        return await _userService.DeleteBatchAsync(idList);
    }

    /// <summary>
    /// 把 "2,3,4" 格式的字符串解析成 long 数组（批量删除用）。
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
