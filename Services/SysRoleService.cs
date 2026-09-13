using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysRole;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 角色服务实现
/// </summary>
public class SysRoleService : ISysRoleService
{
    private readonly IRepository<SysRole> _roleRepository;
    private readonly IRepository<SysUser> _userRepository;
    private readonly IRepository<SysRoleMenu> _roleMenuRepository;
    private readonly ISysMenuRepository _menuRepository;
    private readonly IMapper _mapper;

    public SysRoleService(
        IRepository<SysRole> roleRepository,
        IRepository<SysUser> userRepository,
        IRepository<SysRoleMenu> roleMenuRepository,
        ISysMenuRepository menuRepository,
        IMapper mapper)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _roleMenuRepository = roleMenuRepository;
        _menuRepository = menuRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 获取角色列表（按 Id 升序）
    /// </summary>
    public async Task<ApiResponse<List<SysRoleDto>>> GetListAsync()
    {
        var roles = await _roleRepository.Query()
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ToListAsync();

        var list = _mapper.Map<List<SysRoleDto>>(roles);
        return ApiResponse<List<SysRoleDto>>.Success(list);
    }

    /// <summary>
    /// 根据 ID 获取角色详情
    /// </summary>
    public async Task<ApiResponse<SysRoleDto>> GetByIdAsync(long id)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null)
        {
            return ApiResponse<SysRoleDto>.Fail("角色不存在", 404);
        }

        return ApiResponse<SysRoleDto>.Success(_mapper.Map<SysRoleDto>(role));
    }

    /// <summary>
    /// 修改角色
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(SysRoleUpdateDto dto)
    {
        // 1. 角色必须存在
        var role = await _roleRepository.GetByIdAsync(dto.Id);
        if (role == null)
        {
            return ApiResponse<object>.Fail("角色不存在", 404);
        }

        // 2. 角色编码唯一性校验（排除自己）
        if (await _roleRepository.AnyAsync(r => r.RoleCode == dto.RoleCode && r.Id != dto.Id))
        {
            return ApiResponse<object>.Fail("角色编码已存在");
        }

        // 3. DTO 增量覆盖到已跟踪实体并保存
        _mapper.Map(dto, role);
        await _roleRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = role.Id }, "角色修改成功");
    }

    /// <summary>
    /// 根据 ID 删除角色（单个删除 = 只有一个元素的批量删除，逻辑完全复用）
    /// </summary>
    public Task<ApiResponse<object>> DeleteAsync(long id)
    {
        return DeleteBatchAsync(new[] { id });
    }

    /// <summary>
    /// 批量删除角色。
    /// 保护规则：内置超级管理员角色（role_code=admin）不允许删除；
    /// 角色下存在用户时不允许删除（避免用户变成无角色孤儿）。
    /// 删除角色的同时清理 sys_role_menu 授权记录。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteBatchAsync(long[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return ApiResponse<object>.Fail("请选择要删除的角色");
        }

        // 1. 查出待删除角色
        var roles = await _roleRepository.Query()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

        // 2. 数量对不上说明部分已不存在
        if (roles.Count != ids.Length)
        {
            return ApiResponse<object>.Fail("部分角色不存在或已被删除");
        }

        // 3. 业务保护：内置超管角色不可删
        if (roles.Any(r => r.RoleCode == "admin"))
        {
            return ApiResponse<object>.Fail("内置超级管理员角色不允许删除");
        }

        // 4. 业务保护：角色下挂有用户时不可删
        if (await _userRepository.AnyAsync(u => u.RoleId != null && ids.Contains(u.RoleId.Value)))
        {
            return ApiResponse<object>.Fail("存在已绑定该角色的用户，请先调整用户角色后再删除");
        }

        // 5. 清理角色-菜单授权记录（外键也是级联删除，这里显式删除语义更清晰）
        var roleMenus = await _roleMenuRepository.GetListAsync(rm => ids.Contains(rm.RoleId));
        foreach (var rm in roleMenus)
        {
            _roleMenuRepository.Remove(rm);
        }

        // 6. 删除角色并一次性保存（同一 SaveChanges，保证原子性）
        foreach (var role in roles)
        {
            _roleRepository.Remove(role);
        }
        await _roleRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { deleted = roles.Count }, "删除成功");
    }

    /// <summary>
    /// 给角色分配菜单权限（PUT /api/sys/role/auth）。
    /// 采用"全量替换"策略：先删除该角色的所有旧授权，再写入本次勾选的菜单，
    /// 与常见后台管理框架（如 RuoYi）行为一致，前端只需提交最终勾选结果。
    /// </summary>
    public async Task<ApiResponse<object>> AssignMenusAsync(SysRoleAuthDto dto)
    {
        // 1. 角色必须存在
        var role = await _roleRepository.GetByIdAsync(dto.RoleId);
        if (role == null)
        {
            return ApiResponse<object>.Fail("角色不存在", 404);
        }

        // 2. 菜单ID去重；若传了ID，校验全部存在（防止前端传非法ID脏数据入库）
        var menuIds = dto.MenuIds?.Distinct().ToList() ?? new List<long>();
        if (menuIds.Count > 0)
        {
            var existCount = await _menuRepository.Query()
                .CountAsync(m => menuIds.Contains(m.Id));
            if (existCount != menuIds.Count)
            {
                return ApiResponse<object>.Fail("部分菜单不存在，请刷新页面后重试");
            }
        }

        // 3. 删除该角色的全部旧授权
        var oldRefs = await _roleMenuRepository.GetListAsync(rm => rm.RoleId == dto.RoleId);
        foreach (var rm in oldRefs)
        {
            _roleMenuRepository.Remove(rm);
        }

        // 4. 写入新授权（传空列表 = 清空权限）
        foreach (var menuId in menuIds)
        {
            await _roleMenuRepository.AddAsync(new SysRoleMenu
            {
                RoleId = dto.RoleId,
                MenuId = menuId
            });
        }

        // 5. 删旧+写新在同一个 SaveChanges 中执行，保证原子性
        await _roleMenuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(
            new { roleId = dto.RoleId, menuCount = menuIds.Count }, "权限分配成功");
    }

    /// <summary>
    /// 获取角色已授权的菜单ID集合（用于分配菜单弹窗回显勾选状态）
    /// </summary>
    public async Task<ApiResponse<List<long>>> GetRoleMenuIdsAsync(long roleId)
    {
        // 复用 SysMenuRepository 中已有的查询方法
        var menuIds = await _menuRepository.GetMenuIdsByRoleIdAsync(roleId);
        return ApiResponse<List<long>>.Success(menuIds);
    }
}
