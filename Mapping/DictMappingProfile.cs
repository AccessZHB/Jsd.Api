using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Mapping;

/// <summary>
/// 字典管理模块 —— Entity ↔ DTO 映射配置。
/// AddAutoMapper 会自动扫描整个程序集并注册本 Profile，无需手动登记。
/// </summary>
public class DictMappingProfile : Profile
{
    public DictMappingProfile()
    {
        // ---------- 字典类型 ----------
        CreateMap<SysDictType, DictTypeDto>();

        // 新增：主键/时间/创建人/状态由 Service 显式赋值，映射时全部忽略
        CreateMap<CreateDictTypeDto, SysDictType>()
            .ForMember(e => e.Id, o => o.Ignore())
            .ForMember(e => e.Status, o => o.Ignore())
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore());

        // 修改：增量覆盖已跟踪实体，只放行业务字段
        CreateMap<UpdateDictTypeDto, SysDictType>()
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore());

        // 下拉选项：value 取 dictType 编码、label 取 dictName
        CreateMap<SysDictType, DictTypeOptionDto>()
            .ForMember(d => d.Value, o => o.MapFrom(e => e.DictType))
            .ForMember(d => d.Label, o => o.MapFrom(e => e.DictName));

        // ---------- 字典数据 ----------
        CreateMap<SysDictData, DictDataDto>()
            // 库里 is_default 是 CHAR(1) Y/N，对前端统一成 bool
            .ForMember(d => d.IsDefault, o => o.MapFrom(e => e.IsDefault == "Y"));

        CreateMap<SysDictData, DictDataItem>()
            .ForMember(d => d.Value, o => o.MapFrom(e => e.DictValue))
            .ForMember(d => d.Label, o => o.MapFrom(e => e.DictLabel))
            .ForMember(d => d.Sort, o => o.MapFrom(e => e.DictSort))
            .ForMember(d => d.IsDefault, o => o.MapFrom(e => e.IsDefault == "Y"))
            .ForMember(d => d.CssClass, o => o.MapFrom(e => e.CssClass));

        CreateMap<CreateDictDataDto, SysDictData>()
            .ForMember(e => e.Id, o => o.Ignore())
            .ForMember(e => e.IsDefault, o => o.Ignore())
            .ForMember(e => e.Status, o => o.Ignore())
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore())
            // css_class 在数据库是 NOT NULL，DTO 里可不填，这里兜底成空串避免写入 NULL
            .ForMember(e => e.CssClass, o => o.MapFrom(d => d.CssClass ?? string.Empty));

        CreateMap<UpdateDictDataDto, SysDictData>()
            .ForMember(e => e.IsDefault, o => o.Ignore())
            .ForMember(e => e.Status, o => o.Ignore())
            .ForMember(e => e.CreateBy, o => o.Ignore())
            .ForMember(e => e.CreateTime, o => o.Ignore())
            .ForMember(e => e.UpdateBy, o => o.Ignore())
            .ForMember(e => e.UpdateTime, o => o.Ignore())
            .ForMember(e => e.CssClass, o => o.MapFrom(d => d.CssClass ?? string.Empty));
    }
}
