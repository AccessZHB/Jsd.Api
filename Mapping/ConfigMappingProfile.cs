using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.System;

namespace Jsd.Api.Mapping;

/// <summary>
/// 系统管理 - 系统配置模块 —— Entity ↔ DTO 映射配置。
/// AddAutoMapper 会自动扫描整个程序集并注册本 Profile，无需手动登记。
/// </summary>
public class ConfigMappingProfile : Profile
{
    public ConfigMappingProfile()
    {
        // ---------- 查询出参 ----------
        CreateMap<SysConfig, SysConfigDto>()
            // 中文展示字段由 Service 的 ToDto 统一补齐（需要按 config_type 判断内置）
            .ForMember(d => d.ConfigTypeText, o => o.Ignore())
            .ForMember(d => d.StatusText, o => o.Ignore())
            .ForMember(d => d.IsBuiltIn, o => o.Ignore());

        CreateMap<SysConfig, SysConfigDetailDto>()
            .ForMember(d => d.ConfigTypeText, o => o.Ignore())
            .ForMember(d => d.StatusText, o => o.Ignore())
            .ForMember(d => d.IsBuiltIn, o => o.Ignore());

        // ---------- 新增 ----------
        // 主键 / 创建人 / 时间由 Service 显式赋值（create_time 是 Computed，EF 不会写入）
        CreateMap<CreateSysConfigDto, SysConfig>()
            .ForMember(e => e.Id, o => o.Ignore())
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore())
            // 库里 remark 可空，空字符串按 null 存，避免列表里出现一堆空串
            .ForMember(e => e.Remark, o => o.MapFrom(d => string.IsNullOrWhiteSpace(d.Remark) ? null : d.Remark.Trim()));

        // ---------- 修改 ----------
        // 增量覆盖已跟踪实体：系统内置记录 Service 只赋值 ConfigValue，
        // 这里把 config_type 也忽略掉（内置标记不允许通过接口改）
        CreateMap<UpdateSysConfigDto, SysConfig>()
            .ForMember(e => e.Id, o => o.Ignore())
            .ForMember(e => e.ConfigType, o => o.Ignore())
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore())
            .ForMember(e => e.Remark, o => o.MapFrom(d => string.IsNullOrWhiteSpace(d.Remark) ? null : d.Remark.Trim()));
    }
}
