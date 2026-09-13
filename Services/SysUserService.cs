using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.SysUser;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 系统用户服务实现
/// </summary>
public class SysUserService : ISysUserService
{
    private readonly ISysUserRepository _userRepository;
    private readonly IRepository<SysRole> _roleRepository;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public SysUserService(
        ISysUserRepository userRepository,
        IRepository<SysRole> roleRepository,
        CurrentUserService currentUser,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>
    /// 分页查询用户列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<SysUserListDto>>> GetPagedListAsync(string? keyword, int page, int pageSize)
    {
        // 参数兜底，防止前端传非法值
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, total) = await _userRepository.GetPagedListAsync(keyword, page, pageSize);

        // Entity → DTO（RoleName 由 AutoMapper 从 Role 导航属性映射）
        var list = _mapper.Map<List<SysUserListDto>>(items);

        return ApiResponse<PagedResult<SysUserListDto>>.Success(
            PagedResult<SysUserListDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 新增用户
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(SysUserCreateDto dto)
    {
        // 1. 账号唯一性校验
        if (await _userRepository.AnyAsync(u => u.UserName == dto.UserName))
        {
            return ApiResponse<object>.Fail("登录账号已存在");
        }

        // 2. DTO → Entity（密码字段在 MappingProfile 中被 Ignore，需单独赋值）
        var user = _mapper.Map<SysUser>(dto);

        // 3. 明文密码 BCrypt 加密后入库
        user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // 4. 保存
        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = user.Id }, "用户创建成功");
    }

    /// <summary>
    /// 根据 ID 获取用户详情（Include 角色，用于修改表单回显）
    /// </summary>
    public async Task<ApiResponse<SysUserListDto>> GetByIdAsync(long id)
    {
        var user = await _userRepository.GetWithRoleAsync(id);
        if (user == null)
        {
            return ApiResponse<SysUserListDto>.Fail("用户不存在", 404);
        }

        return ApiResponse<SysUserListDto>.Success(_mapper.Map<SysUserListDto>(user));
    }

    /// <summary>
    /// 修改用户信息（不支持修改密码，改密码建议走单独的"重置密码"接口）
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(SysUserUpdateDto dto)
    {
        // 1. 用户必须存在（GetByIdAsync 查出的是被 EF 跟踪的实体，改完 SaveChanges 即更新）
        var user = await _userRepository.GetByIdAsync(dto.Id);
        if (user == null)
        {
            return ApiResponse<object>.Fail("用户不存在", 404);
        }

        // 2. 账号唯一性校验（排除自己，否则改自己的时候会误报"已存在"）
        if (await _userRepository.AnyAsync(u => u.UserName == dto.UserName && u.Id != dto.Id))
        {
            return ApiResponse<object>.Fail("登录账号已存在");
        }

        // 3. 如果指定了角色，校验角色是否存在
        if (dto.RoleId.HasValue && !await _roleRepository.AnyAsync(r => r.Id == dto.RoleId.Value))
        {
            return ApiResponse<object>.Fail("所选角色不存在");
        }

        // 4. DTO 增量覆盖到已跟踪实体
        //    （Id/Password/Avatar/登录信息/时间字段在 MappingProfile 中被 Ignore，不会被改动）
        _mapper.Map(dto, user);
        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = user.Id }, "用户修改成功");
    }

    /// <summary>
    /// 根据 ID 删除用户（单个删除 = 只有一个元素的批量删除，逻辑完全复用）
    /// </summary>
    public Task<ApiResponse<object>> DeleteAsync(long id)
    {
        return DeleteBatchAsync(new[] { id });
    }

    /// <summary>
    /// 批量删除用户（物理删除）。
    /// 保护规则：超级管理员不允许删除；不能删除当前登录账号。
    /// 所有校验通过后一次性 SaveChanges —— 要么全删，要么全不删。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteBatchAsync(long[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return ApiResponse<object>.Fail("请选择要删除的用户");
        }

        // 1. 查出待删除用户（不加 AsNoTracking，Remove 需要实体）
        var users = await _userRepository.Query()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync();

        // 2. 数量对不上说明部分已不存在
        if (users.Count != ids.Length)
        {
            return ApiResponse<object>.Fail("部分用户不存在或已被删除");
        }

        // 3. 业务保护校验
        if (users.Any(u => u.IsSuper == 1))
        {
            return ApiResponse<object>.Fail("超级管理员账号不允许删除");
        }
        if (ids.Contains(_currentUser.UserId))
        {
            return ApiResponse<object>.Fail("不能删除当前登录账号");
        }

        // 4. 删除并保存
        foreach (var user in users)
        {
            _userRepository.Remove(user);
        }
        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { deleted = users.Count }, "删除成功");
    }
}
