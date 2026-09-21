using FluentValidation;
using Jsd.Api.Models.AfterSale;

namespace Jsd.Api.Validators;

/// <summary>
/// 售后管理模块 —— FluentValidation 校验规则
/// 与采购/财务模块一致：Service 层显式调用 ValidateAndThrow。
/// </summary>

/// <summary>提交退款申请入参校验</summary>
public class CreateRefundValidator : AbstractValidator<CreateRefundDto>
{
    public CreateRefundValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("订单ID不合法");

        RuleFor(x => x.RefundAmount)
            .GreaterThan(0).WithMessage("退款金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("退款金额最多保留 2 位小数");

        RuleFor(x => x.RefundType)
            .InclusiveBetween(1, 2).WithMessage("退款类型不合法（1-仅退款 2-退货退款）");

        RuleFor(x => x.RefundReason)
            .NotEmpty().WithMessage("请填写退款原因")
            .MinimumLength(2).WithMessage("退款原因至少 2 个字")
            .MaximumLength(255).WithMessage("退款原因不能超过 255 字");

        RuleFor(x => x.VoucherImages)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.VoucherImages))
            .WithMessage("凭证图片路径不能超过 500 字");
    }
}

/// <summary>审核退款入参校验</summary>
public class ApproveRefundValidator : AbstractValidator<ApproveRefundDto>
{
    public ApproveRefundValidator()
    {
        RuleFor(x => x.Approved)
            .Must(_ => true).WithMessage("请明确审核结果（通过/驳回）");

        // 驳回时强制填写处理说明
        RuleFor(x => x.AdminRemark)
            .NotEmpty().When(x => !x.Approved)
            .WithMessage("驳回时必须填写处理说明")
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.AdminRemark))
            .WithMessage("处理说明不能超过 255 字");
    }
}

/// <summary>创建退货单入参校验</summary>
public class CreateReturnValidator : AbstractValidator<CreateReturnDto>
{
    public CreateReturnValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("订单ID不合法");

        RuleFor(x => x.MemberId)
            .GreaterThan(0).WithMessage("申请人不合法");

        RuleFor(x => x.ReturnReason)
            .NotEmpty().WithMessage("请填写退货原因")
            .MinimumLength(2).WithMessage("退货原因至少 2 个字")
            .MaximumLength(255).WithMessage("退货原因不能超过 255 字");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("退货明细不能为空");

        RuleForEach(x => x.Items).SetValidator(new ReturnItemInputValidator());

        // 同一订单明细不允许在同一张退货单重复出现（避免库存重复回滚）
        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.OrderItemId).Distinct().Count() == items.Count)
            .WithMessage("退货明细中存在重复的订单明细行");
    }
}

/// <summary>退货明细入参校验</summary>
public class ReturnItemInputValidator : AbstractValidator<ReturnItemInputDto>
{
    public ReturnItemInputValidator()
    {
        RuleFor(x => x.ProdInfoId)
            .GreaterThan(0).WithMessage("商品不合法");

        RuleFor(x => x.ProdInfoName)
            .NotEmpty().WithMessage("商品名称不能为空")
            .MaximumLength(100).WithMessage("商品名称不能超过 100 字");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("退货数量必须大于 0");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("商品单价不能为负")
            .Must(p => decimal.Round(p, 2) == p).WithMessage("商品单价最多保留 2 位小数");
    }
}

/// <summary>仓库确认收货入参校验</summary>
public class ReceiveReturnValidator : AbstractValidator<ReceiveReturnDto>
{
    public ReceiveReturnValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("收货明细不能为空，至少包含一行");

        RuleForEach(x => x.Items).SetValidator(new ReceiveReturnItemValidator());
    }
}

/// <summary>收货明细行校验</summary>
public class ReceiveReturnItemValidator : AbstractValidator<ReceiveReturnItemDto>
{
    public ReceiveReturnItemValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("退货明细行不合法");

        RuleFor(x => x.ReceivedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("实收数量不能为负");
    }
}
