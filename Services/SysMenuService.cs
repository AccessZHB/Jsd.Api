using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysMenu;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 菜单服务实现
/// </summary>
public class SysMenuService : ISysMenuService
{
    private readonly ISysMenuRepository _menuRepository;
    private readonly IRepository<SysUser> _userRepository;
    private readonly IRepository<SysRoleMenu> _roleMenuRepository;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public SysMenuService(
        ISysMenuRepository menuRepository,
        IRepository<SysUser> userRepository,
        IRepository<SysRoleMenu> roleMenuRepository,
        CurrentUserService currentUser,
        IMapper mapper)
    {
        _menuRepository = menuRepository;
        _userRepository = userRepository;
        _roleMenuRepository = roleMenuRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>
    /// 获取当前用户菜单树：
    /// 1. 查出所有可见菜单（目录+菜单）
    /// 2. 超管：全部可见；普通角色：取 sys_role_menu 授权的菜单ID
    /// 3. 补全父级目录（保证子菜单有挂载节点）
    /// 4. 扁平列表组装成树
    /// </summary>
    public async Task<ApiResponse<List<MenuTreeNodeDto>>> GetCurrentUserMenuTreeAsync()
    {
        // 当前登录用户
        var user = await _userRepository.GetByIdAsync(_currentUser.UserId);
        if (user == null)
        {
            return ApiResponse<List<MenuTreeNodeDto>>.Fail("用户不存在或登录已过期", 401);
        }

        // 所有可见菜单（已按 SortOrder 排序）
        var allMenus = await _menuRepository.GetAllVisibleMenusAsync();

        // 计算当前用户能看到的菜单ID集合
        var visibleIds = new HashSet<long>();

        if (user.IsSuper == 1)
        {
            // 超级管理员：拥有全部菜单
            foreach (var m in allMenus) visibleIds.Add(m.Id);
        }
        else if (user.RoleId.HasValue)
        {
            // 普通角色：查角色已授权的菜单ID
            var authorizedIds = await _menuRepository.GetMenuIdsByRoleIdAsync(user.RoleId.Value);

            foreach (var id in authorizedIds)
            {
                visibleIds.Add(id);

                // 向上补全所有父级目录（否则子菜单挂不到树上，导航栏会缺层级）
                var parentId = allMenus.FirstOrDefault(m => m.Id == id)?.ParentId ?? 0;
                while (parentId != 0 && !visibleIds.Contains(parentId))
                {
                    visibleIds.Add(parentId);
                    parentId = allMenus.FirstOrDefault(m => m.Id == parentId)?.ParentId ?? 0;
                }
            }
        }

        // 过滤出可见菜单并组装成树
        var visibleMenus = allMenus.Where(m => visibleIds.Contains(m.Id)).ToList();
        var tree = BuildTree(visibleMenus, rootParentId: 0);

        return ApiResponse<List<MenuTreeNodeDto>>.Success(tree);
    }

    /// <summary>
    /// 根据 ID 获取菜单详情
    /// </summary>
    public async Task<ApiResponse<SysMenuDto>> GetByIdAsync(long id)
    {
        var menu = await _menuRepository.GetByIdAsync(id);
        if (menu == null)
        {
            return ApiResponse<SysMenuDto>.Fail("菜单不存在", 404);
        }

        return ApiResponse<SysMenuDto>.Success(_mapper.Map<SysMenuDto>(menu));
    }

    /// <summary>
    /// 修改菜单
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(SysMenuUpdateDto dto)
    {
        // 1. 菜单必须存在
        var menu = await _menuRepository.GetByIdAsync(dto.Id);
        if (menu == null)
        {
            return ApiResponse<object>.Fail("菜单不存在", 404);
        }

        // 2. 父级不能是自己（否则会形成自己挂自己的死循环树）
        if (dto.ParentId == dto.Id)
        {
            return ApiResponse<object>.Fail("父级菜单不能是当前菜单自己");
        }

        // 3. DTO 增量覆盖到已跟踪实体并保存
        _mapper.Map(dto, menu);
        await _menuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = menu.Id }, "菜单修改成功");
    }

    /// <summary>
    /// 根据 ID 删除菜单（单个删除 = 只有一个元素的批量删除，逻辑完全复用）
    /// </summary>
    public Task<ApiResponse<object>> DeleteAsync(long id)
    {
        return DeleteBatchAsync(new[] { id });
    }

    /// <summary>
    /// 批量删除菜单。
    /// 子菜单保护：只要被删菜单存在"不随本次一起删除"的子菜单，就拒绝删除；
    /// 删除菜单的同时清理 sys_role_menu 中对这些菜单的授权记录。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteBatchAsync(long[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return ApiResponse<object>.Fail("请选择要删除的菜单");
        }

        // 1. 查出待删除菜单
        var menus = await _menuRepository.Query()
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

        // 2. 数量对不上说明部分已不存在
        if (menus.Count != ids.Length)
        {
            return ApiResponse<object>.Fail("部分菜单不存在或已被删除");
        }

        // 3. 子菜单保护：存在"父级在删除列表、但自己不在删除列表"的菜单 → 拒绝删除
        //    （单删一个有子菜单的目录时，该条件即"存在子菜单，请先删除子菜单"）
        if (await _menuRepository.AnyAsync(m => ids.Contains(m.ParentId) && !ids.Contains(m.Id)))
        {
            return ApiResponse<object>.Fail("存在子菜单，请先删除子菜单");
        }

        // 4. 清理角色-菜单授权记录（外键也是级联删除，这里显式删除语义更清晰）
        var roleMenus = await _roleMenuRepository.GetListAsync(rm => ids.Contains(rm.MenuId));
        foreach (var rm in roleMenus)
        {
            _roleMenuRepository.Remove(rm);
        }

        // 5. 删除菜单并一次性保存
        foreach (var menu in menus)
        {
            _menuRepository.Remove(menu);
        }
        await _menuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { deleted = menus.Count }, "删除成功");
    }

    /// <summary>
    /// 递归把扁平菜单列表组装成树形结构
    /// </summary>
    /// <param name="menus">扁平菜单列表</param>
    /// <param name="rootParentId">当前层级的父ID（顶级传 0）</param>
    private List<MenuTreeNodeDto> BuildTree(List<SysMenu> menus, long rootParentId)
    {
        // 按 ParentId 分组，方便取某节点的直接子级
        var lookup = menus.ToLookup(m => m.ParentId);

        List<MenuTreeNodeDto> BuildNodes(long parentId)
        {
            return lookup[parentId]
                .Select(m => new MenuTreeNodeDto
                {
                    Id = m.Id,
                    ParentId = m.ParentId,
                    MenuName = m.MenuName,
                    Path = m.Path,
                    Component = m.Component,
                    Icon = m.Icon,
                    MenuType = m.MenuType,
                    Permission = m.Permission,
                    SortOrder = m.SortOrder,
                    Children = BuildNodes(m.Id)   // 递归挂子菜单
                })
                .ToList();
        }

        return BuildNodes(rootParentId);
    }
}
