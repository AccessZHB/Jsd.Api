using FluentValidation;
using Jsd.Api.Models.Invoice;

namespace Jsd.Api.Validators;

/// <summary>
/// 发票与税务管理模块 —— FluentValidation 校验规则
/// 与售后/财务/采购口径一致：由 Service 层显式调用 ValidateAndThrow(dto)。
///
/// ⚠️⚠️ FluentValidation 重要陷阱（本文件所有规则都遵守）：
///   When(...) 默认作用于【整条 RuleFor 链上的所有验证器】，而不是"紧邻的上一个"。
///   所以下面这种写法是错的 —— 第二个 When 会把第一个验证器的条件也改写掉：
///       RuleFor(x => x.BankName)
///           .NotEmpty().When(是专票)              // 期望：专票必填
///           .MaximumLength(100).When(非空);       // 实际：这个 When 也会套到 NotEmpty 上，
///                                                // 结果变成"只有非空才校验非空" → 必填形同虚设
///   正确做法：一个 RuleFor 链只放一个验证器，条件各异时拆成多条 RuleFor。
///
/// ⚠️ 依赖库表或记录当前状态的规则（如"已开票的红冲必须填蓝票号码"、金额是否超过订单实付）
///    放在 Service 里做，Validator 只做纯入参校验。
/// </summary>

/// <summary>保存客户开票信息入参校验</summary>
public class SaveCustomerInvoiceValidator : AbstractValidator<SaveCustomerInvoiceDto>
{
    public SaveCustomerInvoiceValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("客户ID不合法");

        RuleFor(x => x.TitleType)
            .InclusiveBetween(1, 2).WithMessage("抬头类型不合法（1-个人 2-企业）");

        RuleFor(x => x.TitleName)
            .NotEmpty().WithMessage("抬头名称不能为空")
            .MinimumLength(2).WithMessage("抬头名称至少 2 个字")
            .MaximumLength(100).WithMessage("抬头名称不能超过 100 字");

        // 税号：企业必填且格式合法（15/17/18/20 位数字+大写字母），个人选填但填了就要合法
        RuleFor(x => x.TaxNo)
            .Must((dto, taxNo) =>
                TaxNoRule.IsValid(taxNo, dto.TitleType == (int)InvoiceTitleType.Enterprise, out _))
            .WithMessage((dto, _) =>
                TaxNoRule.IsValid(dto.TaxNo, dto.TitleType == (int)InvoiceTitleType.Enterprise, out var err)
                    ? "纳税人识别号不合法"
                    : err ?? "纳税人识别号不合法");

        RuleFor(x => x.BankName)
            .MaximumLength(100).WithMessage("开户银行不能超过 100 字");

        RuleFor(x => x.BankAccount)
            .MaximumLength(64).WithMessage("银行账号不能超过 64 字");

        RuleFor(x => x.BankAccount)
            .Matches("^[0-9A-Za-z\\- ]+$").When(x => !string.IsNullOrEmpty(x.BankAccount))
            .WithMessage("银行账号只能包含数字、字母、短横线和空格");

        RuleFor(x => x.Address)
            .MaximumLength(200).WithMessage("注册地址不能超过 200 字");

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage("联系电话不能超过 32 字");
    }
}

/// <summary>提交开票申请入参校验</summary>
public class ApplyInvoiceValidator : AbstractValidator<ApplyInvoiceDto>
{
    public ApplyInvoiceValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("订单ID不合法");

        RuleFor(x => x.InvoiceType)
            .InclusiveBetween(1, 2).WithMessage("发票类型不合法（1-增值税电子普通发票 2-增值税专用发票）");

        RuleFor(x => x.TitleType)
            .InclusiveBetween(1, 2).WithMessage("抬头类型不合法（1-个人 2-企业）");

        RuleFor(x => x.TitleName)
            .NotEmpty().WithMessage("发票抬头名称不能为空")
            .MinimumLength(2).WithMessage("发票抬头名称至少 2 个字")
            .MaximumLength(100).WithMessage("发票抬头名称不能超过 100 字");

        RuleFor(x => x.TaxNo)
            .Must((dto, taxNo) =>
                TaxNoRule.IsValid(taxNo, dto.TitleType == (int)InvoiceTitleType.Enterprise, out _))
            .WithMessage((dto, _) =>
                TaxNoRule.IsValid(dto.TaxNo, dto.TitleType == (int)InvoiceTitleType.Enterprise, out var err)
                    ? "纳税人识别号不合法"
                    : err ?? "纳税人识别号不合法");

        // ---- 增值税专用发票：必须企业抬头 ----
        RuleFor(x => x.TitleType)
            .Equal((int)InvoiceTitleType.Enterprise).When(x => x.InvoiceType == (int)InvoiceType.SpecialVat)
            .WithMessage("增值税专用发票必须使用企业抬头");

        // ---- 增值税专用发票：开户行 / 账号 / 地址 / 电话 四项必填 ----
        // 每条规则单独一个 RuleFor（避免 When 互相覆盖，见文件头说明）
        RuleFor(x => x.BankName)
            .NotEmpty().When(x => x.InvoiceType == (int)InvoiceType.SpecialVat)
            .WithMessage("开具增值税专用发票必须填写开户银行");

        RuleFor(x => x.BankName)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.BankName))
            .WithMessage("开户银行不能超过 100 字");

        RuleFor(x => x.BankAccount)
            .NotEmpty().When(x => x.InvoiceType == (int)InvoiceType.SpecialVat)
            .WithMessage("开具增值税专用发票必须填写银行账号");

        RuleFor(x => x.BankAccount)
            .MaximumLength(64).When(x => !string.IsNullOrEmpty(x.BankAccount))
            .WithMessage("银行账号不能超过 64 字");

        RuleFor(x => x.Address)
            .NotEmpty().When(x => x.InvoiceType == (int)InvoiceType.SpecialVat)
            .WithMessage("开具增值税专用发票必须填写注册地址");

        RuleFor(x => x.Address)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Address))
            .WithMessage("注册地址不能超过 200 字");

        RuleFor(x => x.Phone)
            .NotEmpty().When(x => x.InvoiceType == (int)InvoiceType.SpecialVat)
            .WithMessage("开具增值税专用发票必须填写联系电话");

        RuleFor(x => x.Phone)
            .MaximumLength(32).When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("联系电话不能超过 32 字");

        // ---- 金额 ----
        // 0 表示"不传"，由服务端按订单实付金额自动填充；传了就必须为正且最多 2 位小数
        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("发票金额不能为负")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("发票金额最多保留 2 位小数")
            .LessThanOrEqualTo(99999999.99m).WithMessage("发票金额超出允许范围");

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).When(x => x.TaxAmount.HasValue)
            .WithMessage("税额不能为负");

        RuleFor(x => x.TaxAmount)
            .Must(t => !t.HasValue || decimal.Round(t.Value, 2) == t.Value).When(x => x.TaxAmount.HasValue)
            .WithMessage("税额最多保留 2 位小数");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注不能超过 500 字");
    }
}

/// <summary>作废 / 红冲入参校验</summary>
public class CancelInvoiceValidator : AbstractValidator<CancelInvoiceDto>
{
    public CancelInvoiceValidator()
    {
        RuleFor(x => x.RedReason)
            .NotEmpty().WithMessage("作废/红冲原因不能为空")
            .MinimumLength(2).WithMessage("作废/红冲原因至少 2 个字")
            .MaximumLength(200).WithMessage("作废/红冲原因不能超过 200 字");

        // "已开票的红冲必须填蓝票号码" 依赖记录状态，由 Service 判断（Validator 只看入参）
        RuleFor(x => x.BlueInvoiceNo)
            .MaximumLength(64).When(x => !string.IsNullOrEmpty(x.BlueInvoiceNo))
            .WithMessage("关联蓝票号码不能超过 64 字");

        RuleFor(x => x.BlueInvoiceNo)
            .Must(IsDigits).When(x => !string.IsNullOrEmpty(x.BlueInvoiceNo))
            .WithMessage("关联蓝票号码只能是数字");
    }

    private static bool IsDigits(string? value)
        => !string.IsNullOrEmpty(value) && value.All(char.IsAsciiDigit);
}

/// <summary>财务回写票面信息入参校验</summary>
public class IssueInvoiceValidator : AbstractValidator<IssueInvoiceDto>
{
    public IssueInvoiceValidator()
    {
        RuleFor(x => x.InvoiceNo)
            .NotEmpty().WithMessage("发票号码不能为空")
            .Must(IsDigits).WithMessage("发票号码只能是数字")
            .Length(8, 20).WithMessage("发票号码长度不合法（应为 8~20 位）");

        // 全电发票已取消发票代码，故允许留空；填了则按 10~12 位数字校验
        RuleFor(x => x.InvoiceCode)
            .Must(IsDigits).When(x => !string.IsNullOrEmpty(x.InvoiceCode))
            .WithMessage("发票代码只能是数字");

        RuleFor(x => x.InvoiceCode)
            .Length(10, 12).When(x => !string.IsNullOrEmpty(x.InvoiceCode))
            .WithMessage("发票代码长度不合法（应为 10~12 位）");

        RuleFor(x => x.InvoiceDate)
            .LessThanOrEqualTo(_ => DateTime.Today.AddDays(1)).When(x => x.InvoiceDate.HasValue)
            .WithMessage("开票日期不能晚于明天");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注不能超过 500 字");
    }

    private static bool IsDigits(string? value)
        => !string.IsNullOrEmpty(value) && value.All(char.IsAsciiDigit);
}
