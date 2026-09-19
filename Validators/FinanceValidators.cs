using FluentValidation;
using Jsd.Api.Models.Finance;

namespace Jsd.Api.Validators;

/// <summary>
/// 财务结算模块 —— FluentValidation 校验规则
///
/// 说明：本项目已引入 FluentValidation 10.3.6，但未引入 FluentValidation.AspNetCore，
/// 因此不在 MVC 管线里自动拦截，而是由 Service 层显式调用 ValidateAndThrow（见 PaymentService）。
/// 好处是校验失败会抛出 ValidationException，被 Controller 统一捕获并返回 400 + 具体字段错误。
/// </summary>

/// <summary>新增收款单入参校验</summary>
public class PaymentCreateValidator : AbstractValidator<PaymentCreateDto>
{
    public PaymentCreateValidator()
    {
        RuleFor(x => x.MemberId)
            .GreaterThan(0).WithMessage("客户不能为空");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("收款金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("收款金额最多保留 2 位小数");

        RuleFor(x => x.PaymentMethod)
            .Must(m => m >= PaymentMethods.BankTransfer && m <= PaymentMethods.Other)
            .WithMessage("收款方式不合法（1-银行转账 2-支票 3-现金 4-其他）");

        RuleFor(x => x.PaymentDate)
            .NotEmpty().WithMessage("打款/到账日期不能为空")
            .LessThanOrEqualTo(DateTime.Today.AddDays(1)).WithMessage("打款/到账日期不能晚于明天");

        RuleFor(x => x.Voucher)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Voucher))
            .WithMessage("凭证路径长度不能超过 255 个字符");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注长度不能超过 500 个字符");
    }
}

/// <summary>单条核销明细校验</summary>
public class WriteOffDtoValidator : AbstractValidator<WriteOffDto>
{
    public WriteOffDtoValidator()
    {
        RuleFor(x => x.ReceivableId)
            .GreaterThan(0).WithMessage("应收账款ID不合法");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("核销金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("核销金额最多保留 2 位小数");
    }
}

/// <summary>核销入参校验（收款单ID + 明细列表）</summary>
public class WriteOffInputValidator : AbstractValidator<WriteOffInputDto>
{
    public WriteOffInputValidator()
    {
        RuleFor(x => x.PaymentId)
            .GreaterThan(0).WithMessage("收款单ID不合法");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("核销明细不能为空");

        RuleForEach(x => x.Items)
            .SetValidator(new WriteOffDtoValidator());

        // 同一张应收单不允许在同一次核销中重复出现（否则金额会被重复累加）
        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.ReceivableId).Distinct().Count() == items.Count)
            .WithMessage("核销明细中存在重复的应收账款");
    }
}
