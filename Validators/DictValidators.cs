using FluentValidation;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Validators;

/// <summary>
/// 字典管理模块 —— FluentValidation 校验规则
/// 与系统管理模块一致：Service 层显式 ValidateAndThrow（同步扩展方法），Controller 捕获后转 ApiResponse。
/// </summary>

/// <summary>新增字典类型</summary>
public class CreateDictTypeValidator : AbstractValidator<CreateDictTypeDto>
{
    public CreateDictTypeValidator()
    {
        RuleFor(x => x.DictName)
            .NotEmpty().WithMessage("字典名称不能为空")
            .MaximumLength(100).WithMessage("字典名称不能超过 100 个字符");

        RuleFor(x => x.DictType)
            .NotEmpty().WithMessage("字典类型编码不能为空")
            .Matches("^[a-zA-Z][a-zA-Z0-9_]*$")
            .WithMessage("字典类型编码只能以字母开头，且只能包含字母、数字和下划线")
            .MaximumLength(100).WithMessage("字典类型编码不能超过 100 个字符");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}

/// <summary>修改字典类型</summary>
public class UpdateDictTypeValidator : AbstractValidator<UpdateDictTypeDto>
{
    public UpdateDictTypeValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("字典类型ID非法");

        RuleFor(x => x.DictName)
            .NotEmpty().WithMessage("字典名称不能为空")
            .MaximumLength(100).WithMessage("字典名称不能超过 100 个字符");

        RuleFor(x => x.DictType)
            .NotEmpty().WithMessage("字典类型编码不能为空")
            .Matches("^[a-zA-Z][a-zA-Z0-9_]*$")
            .WithMessage("字典类型编码只能以字母开头，且只能包含字母、数字和下划线")
            .MaximumLength(100).WithMessage("字典类型编码不能超过 100 个字符");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}

/// <summary>新增字典数据</summary>
public class CreateDictDataValidator : AbstractValidator<CreateDictDataDto>
{
    public CreateDictDataValidator()
    {
        RuleFor(x => x.DictTypeId).GreaterThan(0).WithMessage("请选择所属的字典类型");

        RuleFor(x => x.DictLabel)
            .NotEmpty().WithMessage("字典标签不能为空")
            .MaximumLength(100).WithMessage("字典标签不能超过 100 个字符");

        RuleFor(x => x.DictValue)
            .NotEmpty().WithMessage("字典键值不能为空")
            .MaximumLength(100).WithMessage("字典键值不能超过 100 个字符");

        RuleFor(x => x.DictSort).GreaterThanOrEqualTo(0).WithMessage("排序值不能为负数");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.CssClass)
            .MaximumLength(100).WithMessage("样式类不能超过 100 个字符");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}

/// <summary>修改字典数据</summary>
public class UpdateDictDataValidator : AbstractValidator<UpdateDictDataDto>
{
    public UpdateDictDataValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("字典数据ID非法");

        RuleFor(x => x.DictTypeId).GreaterThan(0).WithMessage("请选择所属的字典类型");

        RuleFor(x => x.DictLabel)
            .NotEmpty().WithMessage("字典标签不能为空")
            .MaximumLength(100).WithMessage("字典标签不能超过 100 个字符");

        RuleFor(x => x.DictValue)
            .NotEmpty().WithMessage("字典键值不能为空")
            .MaximumLength(100).WithMessage("字典键值不能超过 100 个字符");

        RuleFor(x => x.DictSort).GreaterThanOrEqualTo(0).WithMessage("排序值不能为负数");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 1).WithMessage("状态取值非法（0-正常 1-停用）");

        RuleFor(x => x.CssClass)
            .MaximumLength(100).WithMessage("样式类不能超过 100 个字符");

        RuleFor(x => x.Remark)
            .MaximumLength(500).WithMessage("备注不能超过 500 个字符");
    }
}
