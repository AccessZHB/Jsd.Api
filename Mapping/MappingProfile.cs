using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.SysMenu;
using Jsd.Api.Models.SysRole;
using Jsd.Api.Models.SysUser;

namespace Jsd.Api.Mapping;

/// <summary>
/// AutoMapper 映射配置：Entity（数据库实体） ↔ DTO（接口传输对象）
/// 在 Program.cs 中通过 AddAutoMapper 自动扫描注册
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ---------- 用户实体 → 用户列表 DTO ----------
        // RoleName 来自导航属性 Role（sys_role 表）
        CreateMap<SysUser, SysUserListDto>()
            .ForMember(dto => dto.RoleName,
                       opt => opt.MapFrom(u => u.Role != null ? u.Role.RoleName : null));

        // ---------- 新增用户 DTO → 用户实体 ----------
        // 注意：密码不能直接映射（DTO 是明文，实体需要 BCrypt 密文），
        // 所以 Password 字段忽略，由 Service 层加密后手动赋值；
        // 主键、登录时间、创建时间等也都由数据库/业务维护，一并忽略。
        CreateMap<SysUserCreateDto, SysUser>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.Password, opt => opt.Ignore())
            .ForMember(e => e.Avatar, opt => opt.Ignore())
            .ForMember(e => e.LastLoginTime, opt => opt.Ignore())
            .ForMember(e => e.LastLoginIp, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore())
            .ForMember(e => e.Role, opt => opt.Ignore());

        // ---------- 角色实体 → 角色 DTO（字段名一致，自动映射） ----------
        CreateMap<SysRole, SysRoleDto>();

        // ---------- 修改用户 DTO → 用户实体 ----------
        // 用于 _mapper.Map(dto, entity) 增量覆盖已跟踪的实体，
        // Id/Password/Avatar/登录信息/时间字段/导航属性全部 Ignore，不被误改。
        CreateMap<SysUserUpdateDto, SysUser>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.Password, opt => opt.Ignore())
            .ForMember(e => e.Avatar, opt => opt.Ignore())
            .ForMember(e => e.LastLoginTime, opt => opt.Ignore())
            .ForMember(e => e.LastLoginIp, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore())
            .ForMember(e => e.Role, opt => opt.Ignore());

        // ---------- 修改角色 DTO → 角色实体 ----------
        CreateMap<SysRoleUpdateDto, SysRole>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore())
            .ForMember(e => e.Users, opt => opt.Ignore());

        // ---------- 菜单：实体 → 详情 DTO；修改 DTO → 实体 ----------
        CreateMap<SysMenu, SysMenuDto>();
        CreateMap<SysMenuUpdateDto, SysMenu>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());
    }
}
