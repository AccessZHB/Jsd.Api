using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Task;

namespace Jsd.Api.Models.Mapping;

/// <summary>
/// 自动任务模块 AutoMapper 配置（仅映射实体与 DTO 的同名字段）。
/// Quartz 侧的下次触发时间、状态文案等派生字段由 Service 层手动补全 ——
/// 这些字段要么要访问 Scheduler，要么依赖枚举文案映射，放进 Profile 会让配置依赖变重。
/// </summary>
public class TaskMappingProfile : Profile
{
    public TaskMappingProfile()
    {
        CreateMap<SysJob, JobDto>();
        CreateMap<SysJobLog, JobLogDto>();
    }
}
