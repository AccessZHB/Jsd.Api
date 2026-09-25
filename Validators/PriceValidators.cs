using FluentValidation;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Validators;

/// <summary>
/// 价格策略与会员余额模块 —— FluentValidation 校验规则
/// （与财务/采购/售后模块一致：Service 层显式 ValidateAndThrow，Controller 只捕获异常）
/// </summary>

/// <summary>新增客户等级入参校验</summary>
public class CreateMemberLevelValidator : AbstractValidator<CreateMemberLevelDto>
{
    public CreateMemberLevelValidator()
    {
        RuleFor(x => x.LevelName)
            .NotEmpty().WithMessage("等级名称不能为空")
            .MaximumLength(50).WithMessage("等级名称不能超过 50 个字符");

        RuleFor(x => x.LevelCode)
            .NotEmpty().WithMessage("等级编码不能为空")
            .MaximumLength(20).WithMessage("等级编码不能超过 20 个字符")
            .Matches("^[A-Za-z0-9_]+$").WithMessage("等级编码只能包含字母、数字和下划线");

        RuleFor(x => x.DefaultDiscount)
            .GreaterThan(0).WithMessage("默认折扣率必须大于 0")
            .LessThanOrEqualTo(1).WithMessage("默认折扣率不能大于 1")
            .Must(d => decimal.Round(d, 4) == d).WithMessage("默认折扣率最多保留 4 位小数");

        RuleFor(x => x.Description)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("描述不能超过 255 个字符");

        RuleFor(x => x.Status)
            .Must(s => s == 0 || s == 1).WithMessage("状态非法（0-停用 1-启用）");
    }
}

/// <summary>更新客户等级入参校验</summary>
public class UpdateMemberLevelValidator : AbstractValidator<UpdateMemberLevelDto>
{
    public UpdateMemberLevelValidator()
    {
        RuleFor(x => x.LevelName)
            .NotEmpty().WithMessage("等级名称不能为空")
            .MaximumLength(50).WithMessage("等级名称不能超过 50 个字符");

        RuleFor(x => x.LevelCode)
            .NotEmpty().WithMessage("等级编码不能为空")
            .MaximumLength(20).WithMessage("等级编码不能超过 20 个字符")
            .Matches("^[A-Za-z0-9_]+$").WithMessage("等级编码只能包含字母、数字和下划线");

        RuleFor(x => x.DefaultDiscount)
            .GreaterThan(0).WithMessage("默认折扣率必须大于 0")
            .LessThanOrEqualTo(1).WithMessage("默认折扣率不能大于 1")
            .Must(d => decimal.Round(d, 4) == d).WithMessage("默认折扣率最多保留 4 位小数");

        RuleFor(x => x.Description)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("描述不能超过 255 个字符");

        RuleFor(x => x.Status)
            .Must(s => s == 0 || s == 1).WithMessage("状态非法（0-停用 1-启用）");
    }
}

/// <summary>新增价格策略入参校验</summary>
public class CreatePriceStrategyValidator : AbstractValidator<CreatePriceStrategyDto>
{
    public CreatePriceStrategyValidator()
    {
        RuleFor(x => x.StrategyName)
            .NotEmpty().WithMessage("策略名称不能为空")
            .MaximumLength(100).WithMessage("策略名称不能超过 100 个字符");

        RuleFor(x => x.StrategyType)
            .Must(t => t is >= 1 and <= 4)
            .WithMessage("策略类型非法（1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价）");

        RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(0).WithMessage("优先级不能为负数");

        RuleFor(x => x.ExpireDate)
            .GreaterThan(x => x.EffectiveDate).WithMessage("失效时间必须晚于生效时间");

        RuleFor(x => x.Description)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("描述不能超过 255 个字符");
    }
}

/// <summary>更新价格策略入参校验（仅草稿可改）</summary>
public class UpdatePriceStrategyValidator : AbstractValidator<UpdatePriceStrategyDto>
{
    public UpdatePriceStrategyValidator()
    {
        RuleFor(x => x.StrategyName)
            .NotEmpty().WithMessage("策略名称不能为空")
            .MaximumLength(100).WithMessage("策略名称不能超过 100 个字符");

        RuleFor(x => x.StrategyType)
            .Must(t => t is >= 1 and <= 4)
            .WithMessage("策略类型非法（1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价）");

        RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(0).WithMessage("优先级不能为负数");

        RuleFor(x => x.ExpireDate)
            .GreaterThan(x => x.EffectiveDate).WithMessage("失效时间必须晚于生效时间");

        RuleFor(x => x.Description)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("描述不能超过 255 个字符");
    }
}

/// <summary>价格规则明细入参校验</summary>
public class PriceRuleItemInputValidator : AbstractValidator<PriceRuleItemInputDto>
{
    public PriceRuleItemInputValidator()
    {
        RuleFor(x => x.MaterialId)
            .GreaterThan(0).WithMessage("商品物料ID不合法");

        RuleFor(x => x.TierNo)
            .GreaterThan(0).WithMessage("阶梯序号必须大于 0");

        RuleFor(x => x.MinQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("最小数量不能为负数");

        RuleFor(x => x.MaxQuantity)
            .GreaterThanOrEqualTo(x => x.MinQuantity).When(x => x.MaxQuantity.HasValue && x.MaxQuantity > 0)
            .WithMessage("最大数量不能小于最小数量");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).When(x => x.Price.HasValue)
            .WithMessage("阶梯单价不能为负数");

        RuleFor(x => x.Discount)
            .GreaterThan(0).LessThanOrEqualTo(1).When(x => x.Discount.HasValue)
            .WithMessage("阶梯折扣必须在 0~1 之间");

        RuleFor(x => x.ReduceAmount)
            .GreaterThanOrEqualTo(0).When(x => x.ReduceAmount.HasValue)
            .WithMessage("阶梯减免金额不能为负数");
    }
}

/// <summary>价格规则入参校验</summary>
public class PriceRuleInputValidator : AbstractValidator<PriceRuleInputDto>
{
    public PriceRuleInputValidator()
    {
        RuleFor(x => x.ApplyScope)
            .Must(s => s is >= 1 and <= 3)
            .WithMessage("适用范围非法（1-全部商品 2-指定分类 3-指定商品）");

        RuleFor(x => x.CalcType)
            .Must(t => t is >= 1 and <= 3)
            .WithMessage("计算方式非法（1-固定价 2-折扣率 3-减免金额）");

        // 折扣率口径：0 < value <= 1；固定价/减免口径：value >= 0
        RuleFor(x => x.CalcValue)
            .GreaterThan(0).LessThanOrEqualTo(1).When(x => x.CalcType == (int)CalcType.Discount)
            .WithMessage("折扣率必须在 0~1 之间（如 0.85 表示 85 折）");

        RuleFor(x => x.CalcValue)
            .GreaterThanOrEqualTo(0).When(x => x.CalcType != (int)CalcType.Discount)
            .WithMessage("计算值不能为负数");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue)
            .WithMessage("底价保护不能为负数");

        RuleFor(x => x.Status)
            .Must(s => s == 0 || s == 1).WithMessage("规则状态非法（0-停用 1-启用）");

        // 指定分类必须填分类ID；指定商品必须至少一条明细
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).When(x => x.ApplyScope == (int)ApplyScope.Category)
            .WithMessage("适用范围为指定分类时，分类ID不能为空");

        RuleFor(x => x.Items)
            .NotEmpty().When(x => x.ApplyScope == (int)ApplyScope.Material)
            .WithMessage("适用范围为指定商品时，商品明细不能为空");

        RuleForEach(x => x.Items).SetValidator(new PriceRuleItemInputValidator());
    }
}

/// <summary>批量维护规则入参校验</summary>
public class PriceRuleBatchSaveValidator : AbstractValidator<PriceRuleBatchSaveDto>
{
    public PriceRuleBatchSaveValidator()
    {
        RuleFor(x => x.Rules)
            .NotEmpty().WithMessage("规则列表不能为空（全量覆盖时传空数组表示清空）");

        RuleForEach(x => x.Rules).SetValidator(new PriceRuleInputValidator());

        RuleFor(x => x.Reason)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("变更原因不能超过 255 个字符");
    }
}

/// <summary>取价请求入参校验</summary>
public class QuotationRequestValidator : AbstractValidator<QuotationRequestDto>
{
    public QuotationRequestValidator()
    {
        RuleFor(x => x.MemMemberId)
            .GreaterThan(0).WithMessage("会员ID不合法");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("取价商品清单不能为空");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MaterialId).GreaterThan(0).WithMessage("商品物料ID不合法");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("购买数量必须大于 0");
        });
    }
}

/// <summary>创建充值单入参校验</summary>
public class RechargeCreateValidator : AbstractValidator<RechargeCreateDto>
{
    public RechargeCreateValidator()
    {
        RuleFor(x => x.MemMemberId)
            .GreaterThan(0).WithMessage("会员ID不合法");

        RuleFor(x => x.RechargeAmount)
            .GreaterThan(0).WithMessage("充值金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("充值金额最多保留 2 位小数")
            .LessThanOrEqualTo(1000000).WithMessage("单笔充值金额不能超过 1,000,000 元");

        RuleFor(x => x.GiftAmount)
            .GreaterThanOrEqualTo(0).WithMessage("赠送金额不能为负数")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("赠送金额最多保留 2 位小数");
    }
}

/// <summary>后台调整余额入参校验</summary>
public class AdminAdjustBalanceValidator : AbstractValidator<AdminAdjustBalanceDto>
{
    public AdminAdjustBalanceValidator()
    {
        RuleFor(x => x.MemMemberId)
            .GreaterThan(0).WithMessage("会员ID不合法");

        RuleFor(x => x.Amount)
            .NotEqual(0).WithMessage("调整金额不能为 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("调整金额最多保留 2 位小数");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("调整原因必填（资金操作必须可审计）")
            .MaximumLength(255).WithMessage("调整原因不能超过 255 个字符");
    }
}

/// <summary>余额冻结入参校验</summary>
public class FreezeBalanceValidator : AbstractValidator<FreezeBalanceDto>
{
    public FreezeBalanceValidator()
    {
        RuleFor(x => x.MemMemberId).GreaterThan(0).WithMessage("会员ID不合法");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("冻结金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("冻结金额最多保留 2 位小数");
        RuleFor(x => x.Remark)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注不能超过 255 个字符");
    }
}

/// <summary>余额解冻入参校验</summary>
public class UnfreezeBalanceValidator : AbstractValidator<UnfreezeBalanceDto>
{
    public UnfreezeBalanceValidator()
    {
        RuleFor(x => x.MemMemberId).GreaterThan(0).WithMessage("会员ID不合法");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("解冻金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("解冻金额最多保留 2 位小数");
        RuleFor(x => x.Remark)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注不能超过 255 个字符");
    }
}

/// <summary>余额扣减入参校验</summary>
public class DeductBalanceValidator : AbstractValidator<DeductBalanceDto>
{
    public DeductBalanceValidator()
    {
        RuleFor(x => x.MemMemberId).GreaterThan(0).WithMessage("会员ID不合法");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("扣减金额必须大于 0")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("扣减金额最多保留 2 位小数");
        RuleFor(x => x.Remark)
            .MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Remark))
            .WithMessage("备注不能超过 255 个字符");
    }
}
