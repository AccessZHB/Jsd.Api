using FluentValidation;
using Jsd.Api.Models.System;

namespace Jsd.Api.Validators;

/// <summary>
/// 系统管理模块 —— FluentValidation 校验规则
/// （与财务/采购/售后/价格模块一致：Service 层显式 ValidateAndThrow，Controller 捕获异常）
/// </summary>

/// <summary>批量删除日志入参校验</summary>
public class BatchDeleteValidator : AbstractValidator<BatchDeleteDto>
{
    public BatchDeleteValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage("请至少选择一条要删除的日志")
            .Must(ids => ids.Count <= 1000).WithMessage("单次最多删除 1000 条");
        RuleForEach(x => x.Ids).GreaterThan(0).WithMessage("日志ID非法");
    }
}

/// <summary>清理过期日志入参校验</summary>
public class CleanLogValidator : AbstractValidator<CleanLogDto>
{
    public CleanLogValidator()
    {
        RuleFor(x => x.Days)
            .InclusiveBetween(1, 3650).WithMessage("保留天数必须在 1~3650 之间");
    }
}

/// <summary>登录安全策略配置校验</summary>
public class SecurityConfigValidator : AbstractValidator<SecurityConfigDto>
{
    public SecurityConfigValidator()
    {
        RuleFor(x => x.MaxLoginAttempts)
            .InclusiveBetween(1, 50).WithMessage("失败锁定阈值必须在 1~50 次之间");
        RuleFor(x => x.LockDurationMinutes)
            .InclusiveBetween(1, 1440).WithMessage("锁定时长必须在 1~1440 分钟之间");
        RuleFor(x => x.PasswordMinLength)
            .InclusiveBetween(6, 64).WithMessage("密码最小长度必须在 6~64 位之间");
    }
}

/// <summary>修改密码入参校验（强度细节由 SecurityService 按安全策略配置校验）</summary>
public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("旧密码不能为空");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不能为空");
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("确认新密码不能为空")
            .Equal(x => x.NewPassword).WithMessage("两次输入的新密码不一致");
    }
}

/// <summary>刷新 Token 入参校验</summary>
public class RefreshTokenValidator : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("refreshToken 不能为空");
    }
}
