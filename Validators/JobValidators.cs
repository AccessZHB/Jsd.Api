using FluentValidation;
using Jsd.Api.Models.Task;

namespace Jsd.Api.Validators;

/// <summary>
/// 自动任务（定时任务）模块 —— FluentValidation 校验规则
/// 与字典 / 系统配置模块一致：Service 层显式 ValidateAndThrow（同步扩展方法），Controller 捕获后转 ApiResponse。
///
/// ⚠️ 只做"格式层面"的校验（非空、长度、取值域、字段个数）。
///    Cron 合法性与 job_type 类存在性属于"业务级校验"，Service 里用 Quartz 的
///    CronExpression / Type.GetType 复核（文档避坑第 1、2 条），这里不重复实现。
/// </summary>

/// <summary>新增定时任务</summary>
public class CreateJobValidator : AbstractValidator<CreateJobDto>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.JobName)
            .NotEmpty().WithMessage("任务名称不能为空")
            .MaximumLength(100).WithMessage("任务名称不能超过 100 个字符");

        RuleFor(x => x.JobGroup)
            .NotEmpty().WithMessage("任务分组不能为空")
            .MaximumLength(100).WithMessage("任务分组不能超过 100 个字符");

        RuleFor(x => x.JobType)
            .NotEmpty().WithMessage("任务类型（全限定类名）不能为空")
            .MaximumLength(200).WithMessage("任务类型不能超过 200 个字符");

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("Cron 表达式不能为空")
            .MaximumLength(100).WithMessage("Cron 表达式不能超过 100 个字符");

        RuleFor(x => x.JobParams)
            .MaximumLength(500).WithMessage("任务参数不能超过 500 个字符");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-停止 1-运行中）");

        RuleFor(x => x.MisfirePolicy)
            .InclusiveBetween(0, 2).WithMessage("错过策略取值非法（0-立即执行 1-执行一次 2-忽略）");

        RuleFor(x => x.Concurrent)
            .InclusiveBetween(0, 1).WithMessage("并发策略取值非法（0-禁止 1-允许）");

        RuleFor(x => x.Timeout)
            .GreaterThanOrEqualTo(0).WithMessage("超时时间不能为负数（0 表示不限制）");

        RuleFor(x => x.RetryCount)
            .GreaterThanOrEqualTo(0).WithMessage("重试次数不能为负数");

        RuleFor(x => x.RetryInterval)
            .GreaterThanOrEqualTo(0).WithMessage("重试间隔不能为负数");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}

/// <summary>修改定时任务</summary>
public class UpdateJobValidator : AbstractValidator<UpdateJobDto>
{
    public UpdateJobValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("任务ID非法");

        RuleFor(x => x.JobName)
            .NotEmpty().WithMessage("任务名称不能为空")
            .MaximumLength(100).WithMessage("任务名称不能超过 100 个字符");

        RuleFor(x => x.JobGroup)
            .NotEmpty().WithMessage("任务分组不能为空")
            .MaximumLength(100).WithMessage("任务分组不能超过 100 个字符");

        RuleFor(x => x.JobType)
            .NotEmpty().WithMessage("任务类型（全限定类名）不能为空")
            .MaximumLength(200).WithMessage("任务类型不能超过 200 个字符");

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("Cron 表达式不能为空")
            .MaximumLength(100).WithMessage("Cron 表达式不能超过 100 个字符");

        RuleFor(x => x.JobParams)
            .MaximumLength(500).WithMessage("任务参数不能超过 500 个字符");

        RuleFor(x => x.MisfirePolicy)
            .InclusiveBetween(0, 2).WithMessage("错过策略取值非法（0-立即执行 1-执行一次 2-忽略）");

        RuleFor(x => x.Concurrent)
            .InclusiveBetween(0, 1).WithMessage("并发策略取值非法（0-禁止 1-允许）");

        RuleFor(x => x.Timeout)
            .GreaterThanOrEqualTo(0).WithMessage("超时时间不能为负数（0 表示不限制）");

        RuleFor(x => x.RetryCount)
            .GreaterThanOrEqualTo(0).WithMessage("重试次数不能为负数");

        RuleFor(x => x.RetryInterval)
            .GreaterThanOrEqualTo(0).WithMessage("重试间隔不能为负数");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}

/// <summary>执行日志分页查询（页码 / 每页条数 / 时间范围的基本合法性）</summary>
public class JobLogQueryValidator : AbstractValidator<JobLogQueryDto>
{
    public JobLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("页码必须从 1 开始");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("每页条数必须大于 0")
            .LessThanOrEqualTo(200).WithMessage("每页条数不能超过 200");

        RuleFor(x => x.FireTimeStart)
            .Must((dto, start, ctx) => start is null || dto.FireTimeEnd is null || start <= dto.FireTimeEnd)
            .WithMessage("触发时间起不能晚于止");

        RuleFor(x => x.JobName)
            .MaximumLength(100).WithMessage("任务名称搜索条件不能超过 100 个字符");
    }
}
