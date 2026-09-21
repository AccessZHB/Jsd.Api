using FluentValidation;
using Jsd.Api.Models.Purchase;

namespace Jsd.Api.Validators;

/// <summary>
/// 采购管理模块 —— FluentValidation 校验规则（与财务模块一致：Service 层显式 ValidateAndThrow）。
/// </summary>

/// <summary>采购订单明细校验（创建/更新复用）</summary>
public class PurchaseOrderItemValidator : AbstractValidator<PurchaseOrderItemInputDto>
{
    public PurchaseOrderItemValidator()
    {
        RuleFor(x => x.MaterialId)
            .GreaterThan(0).WithMessage("物料/SKU 不合法");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("采购数量必须大于 0")
            .Must(q => decimal.Round(q, 2) == q).WithMessage("采购数量最多保留 2 位小数");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("采购单价不能为负")
            .Must(p => decimal.Round(p, 2) == p).WithMessage("采购单价最多保留 2 位小数");

        RuleFor(x => x.Remark)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("明细备注长度不能超过 255 个字符");
    }
}

/// <summary>创建采购订单入参校验</summary>
public class PurchaseOrderCreateValidator : AbstractValidator<CreatePurchaseOrderDto>
{
    public PurchaseOrderCreateValidator()
    {
        RuleFor(x => x.SupplierId)
            .GreaterThan(0).WithMessage("供应商不能为空");

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage("下单日期不能为空");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注长度不能超过 500 个字符");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("采购明细不能为空");

        RuleForEach(x => x.Items)
            .SetValidator(new PurchaseOrderItemValidator());

        // 同一 SKU 不允许在同一张订单重复出现（否则金额/入库会被重复计算）
        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.MaterialId).Distinct().Count() == items.Count)
            .WithMessage("采购明细中存在重复的物料(SKU)");
    }
}

/// <summary>创建入库单入参校验</summary>
public class InboundCreateValidator : AbstractValidator<CreateInboundDto>
{
    public InboundCreateValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("采购订单不能为空");

        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("仓库不能为空");

        RuleFor(x => x.InboundDate)
            .NotEmpty().WithMessage("入库日期不能为空");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注长度不能超过 500 个字符");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("入库明细不能为空");

        RuleForEach(x => x.Items).SetValidator(new InboundItemValidator());

        // 同一订单明细不允许在同一张入库单重复出现
        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.OrderItemId).Distinct().Count() == items.Count)
            .WithMessage("入库明细中存在重复的采购订单明细行");
    }
}

/// <summary>入库明细校验</summary>
public class InboundItemValidator : AbstractValidator<InboundItemInputDto>
{
    public InboundItemValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0).WithMessage("采购订单明细行不合法");

        RuleFor(x => x.ActualQuantity)
            .GreaterThan(0).WithMessage("实收数量必须大于 0")
            .Must(q => decimal.Round(q, 2) == q).WithMessage("实收数量最多保留 2 位小数");
    }
}

/// <summary>付款核销入参校验</summary>
public class PayPaymentValidator : AbstractValidator<PayPaymentDto>
{
    public PayPaymentValidator()
    {
        RuleFor(x => x.PayAmount)
            .GreaterThan(0).WithMessage("付款金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("付款金额最多保留 2 位小数");

        RuleFor(x => x.PayMethod)
            .InclusiveBetween(1, 4).WithMessage("付款方式不合法（1-银行转账 2-支票 3-现金 4-其他）");

        RuleFor(x => x.Remark)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注长度不能超过 500 个字符");
    }
}
