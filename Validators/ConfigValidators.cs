using FluentValidation;
using Jsd.Api.Models.System;

namespace Jsd.Api.Validators;

/// <summary>
/// 系统管理 - 系统配置模块 —— FluentValidation 校验规则
/// 与字典模块一致：Service 层显式 ValidateAndThrow（同步扩展方法），Controller 捕获后转 ApiResponse。
/// </summary>

/// <summary>新增系统配置</summary>
public class CreateSysConfigValidator : AbstractValidator<CreateSysConfigDto>
{
    /// <summary>
    /// 配置键命名规范：模块.功能.参数，点分驼峰小写段，每段以字母开头。
    /// 文档举例 sys.login.fail.lock.count 是 5 段，所以这里只约束段内字符、不限定段数。
    /// </summary>
    public const string ConfigKeyPattern = "^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)*$";

    public CreateSysConfigValidator()
    {
        RuleFor(x => x.ConfigName)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(100).WithMessage("配置名称不能超过 100 个字符");

        RuleFor(x => x.ConfigKey)
            .NotEmpty().WithMessage("配置键名不能为空")
            .MaximumLength(100).WithMessage("配置键名不能超过 100 个字符")
            .Matches(ConfigKeyPattern)
            .WithMessage("配置键名需符合 模块.功能.参数 规范（每段以字母开头，仅含字母、数字、下划线）");

        RuleFor(x => x.ConfigValue)
            .MaximumLength(500).WithMessage("配置值不能超过 500 个字符");

        RuleFor(x => x.ConfigType)
            .Must(t => t == ConfigTypeConst.BuiltIn || t == ConfigTypeConst.Custom)
            .WithMessage("配置类型取值非法（Y-系统内置 N-自定义）");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.Remark!)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符")
            .When(x => !string.IsNullOrEmpty(x.Remark));
    }
}

/// <summary>修改系统配置</summary>
public class UpdateSysConfigValidator : AbstractValidator<UpdateSysConfigDto>
{
    public UpdateSysConfigValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("配置ID非法");

        RuleFor(x => x.ConfigName)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(100).WithMessage("配置名称不能超过 100 个字符");

        RuleFor(x => x.ConfigKey)
            .NotEmpty().WithMessage("配置键名不能为空")
            .MaximumLength(100).WithMessage("配置键名不能超过 100 个字符")
            .Matches(CreateSysConfigValidator.ConfigKeyPattern)
            .WithMessage("配置键名需符合 模块.功能.参数 规范（每段以字母开头，仅含字母、数字、下划线）");

        RuleFor(x => x.ConfigValue)
            .MaximumLength(500).WithMessage("配置值不能超过 500 个字符");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.Remark!)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符")
            .When(x => !string.IsNullOrEmpty(x.Remark));
    }
}

/// <summary>批量获取配置值</summary>
public class ConfigKeysValidator : AbstractValidator<ConfigKeysDto>
{
    public ConfigKeysValidator()
    {
        RuleFor(x => x.Keys)
            .NotEmpty().WithMessage("配置键名列表不能为空")
            .Must(k => k.Count <= 100).WithMessage("一次最多批量获取 100 个配置");

        RuleForEach(x => x.Keys)
            .NotEmpty().WithMessage("配置键名不能为空")
            .MaximumLength(100).WithMessage("配置键名不能超过 100 个字符");
    }
}
