using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Order;

// ============================================================
// 确认订单页 DTO：收货地址 / 预计算 / 单事务提交
// ============================================================

/// <summary>收货地址输出（GET /api/address/list）</summary>
public class AddressDto
{
    /// <summary>地址ID</summary>
    public long Id { get; set; }

    /// <summary>会员ID</summary>
    public long MemberId { get; set; }

    /// <summary>收货人</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>电话</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>省</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>市</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>区/县</summary>
    public string District { get; set; } = string.Empty;

    /// <summary>详细地址</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>是否默认</summary>
    public bool IsDefault { get; set; }
}

/// <summary>保存收货地址入参（POST /api/address/save；Id=0 表示新增）</summary>
public class AddressSaveDto
{
    /// <summary>地址ID，0=新增</summary>
    public long Id { get; set; }

    /// <summary>收货人姓名</summary>
    [Required(ErrorMessage = "收货人姓名不能为空")]
    [StringLength(50, ErrorMessage = "收货人姓名不能超过50个字符")]
    public string Name { get; set; } = string.Empty;

    /// <summary>收货人电话</summary>
    [Required(ErrorMessage = "收货人电话不能为空")]
    [StringLength(20, ErrorMessage = "收货人电话不能超过20个字符")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>省</summary>
    [StringLength(50)]
    public string? Province { get; set; }

    /// <summary>市</summary>
    [StringLength(50)]
    public string? City { get; set; }

    /// <summary>区/县</summary>
    [StringLength(50)]
    public string? District { get; set; }

    /// <summary>详细地址</summary>
    [Required(ErrorMessage = "详细地址不能为空")]
    [StringLength(255, ErrorMessage = "详细地址不能超过255个字符")]
    public string Detail { get; set; } = string.Empty;

    /// <summary>是否设为默认地址</summary>
    public bool IsDefault { get; set; }
}

/// <summary>删除收货地址入参（POST /api/address/delete；按当前登录会员校验归属）</summary>
public class AddressDeleteDto
{
    /// <summary>要删除的地址ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "地址ID非法")]
    public long Id { get; set; }
}

/// <summary>订单明细入参（预计算 / 提交共用）：只需 SKU + 数量 + 前端透传的规格/光度 JSON</summary>
public class OrderSubmitItemDto
{
    /// <summary>SKU ID（sku_id）</summary>
    [Required(ErrorMessage = "SKU ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "SKU ID非法")]
    public long SkuId { get; set; }

    /// <summary>购买数量</summary>
    [Range(1, int.MaxValue, ErrorMessage = "购买数量必须大于0")]
    public int Quantity { get; set; } = 1;

    /// <summary>规格 + 光度 JSON（前端透传，落库到 trx_order_item.spec_json）</summary>
    public string? SpecJson { get; set; }

    /// <summary>加工状态：unprocessed-待加工 / processed-已加工（默认 processed）</summary>
    public string? ProcessingStatus { get; set; }
}

/// <summary>订单预计算入参（POST /api/order/pre-calculate；确认页初始化用）</summary>
/// <summary>
/// 订单预计算入参（POST /api/order/pre-calculate）
///
/// 【金额安全 F-31】优惠/运费是前端传入的**唯一**金额入参（单价由后端价格引擎重算），
/// 因此必须做边界校验，绝不能让"优惠大于总额"通过 —— 否则确认页会显示 0 元，
/// 用户随后提交即可 0 元拿货。校验分两层：
///   1) 本 DTO 的 IValidatableObject：非负、绝对上限、以及与前端回传的 TotalAmount 自检；
///   2) OrderService.PreCalculateAsync：以**服务端自算的 totalAmount** 为准做最终裁决（权威）。
/// </summary>
public class OrderPreCalculateDto : IValidatableObject
{
    /// <summary>订单明细</summary>
    [Required(ErrorMessage = "订单明细不能为空")]
    [MinLength(1, ErrorMessage = "订单明细至少需要一条")]
    public List<OrderSubmitItemDto> Items { get; set; } = new();

    /// <summary>优惠金额（可选，默认 0）</summary>
    [Range(0, double.MaxValue, ErrorMessage = "优惠金额不能为负数")]
    public decimal DiscountAmount { get; set; }

    /// <summary>运费（可选，默认 0）</summary>
    [Range(0, double.MaxValue, ErrorMessage = "运费金额不能为负数")]
    public decimal FreightAmount { get; set; }

    /// <summary>
    /// 商品总价（可选，仅用于前端自检/前置校验）。
    /// ⚠️ 服务端**不信任**此字段的绝对值，最终以价格引擎重算的总额为准。
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in MoneyValidation.ValidateAmounts(
                     DiscountAmount, FreightAmount, TotalAmount, nameof(DiscountAmount), nameof(FreightAmount)))
        {
            yield return error;
        }
    }
}

/// <summary>
/// 订单金额入参的公共校验规则。
///
/// 【背景 · F-31 资损漏洞】原实现只做 `if (payAmount &lt; 0) payAmount = 0;` 的被动归零，
/// 前端传入 `discountAmount = 999999` 即可把应付金额压成 0，而服务端仍会扣库存、置已支付、结清应收 → 0 元拿货。
/// 现在改为：DTO 层拦截明显非法值，Service 层用**服务端自算总额**做最终裁决（见 OrderService）。
/// </summary>
internal static class MoneyValidation
{
    /// <summary>单笔优惠/运费的绝对上限（业务兜底，防误填与数值溢出）</summary>
    public const decimal MaxAmount = 1_000_000m;

    /// <summary>
    /// 校验优惠与运费金额。
    /// </summary>
    /// <param name="discount">优惠金额</param>
    /// <param name="freight">运费</param>
    /// <param name="totalAmount">商品总价（前端回传，可为空；非空且 &gt;0 时做交叉校验）</param>
    /// <param name="discountMember">优惠字段名（用于把错误挂到具体字段）</param>
    /// <param name="freightMember">运费字段名</param>
    public static IEnumerable<ValidationResult> ValidateAmounts(
        decimal discount, decimal freight, decimal? totalAmount, string discountMember, string freightMember)
    {
        if (discount < 0)
        {
            yield return new ValidationResult("优惠金额不能为负数", new[] { discountMember });
        }
        if (freight < 0)
        {
            yield return new ValidationResult("运费金额不能为负数", new[] { freightMember });
        }
        if (discount > MaxAmount)
        {
            yield return new ValidationResult(
                $"优惠金额超出允许上限（单笔不超过 {MaxAmount:0.##} 元）", new[] { discountMember });
        }
        if (freight > MaxAmount)
        {
            yield return new ValidationResult(
                $"运费金额超出允许上限（单笔不超过 {MaxAmount:0.##} 元）", new[] { freightMember });
        }
        // 交叉校验：仅当调用方提供了总价时生效（最终仍以服务端重算总额为准）
        if (totalAmount is > 0 && discount > totalAmount.Value)
        {
            yield return new ValidationResult(
                $"优惠金额（{discount:0.##}）不能大于商品总价（{totalAmount.Value:0.##}）",
                new[] { discountMember });
        }
    }
}

/// <summary>预计算明细行</summary>
public class OrderPreCalculateItemDto
{
    public long SkuId { get; set; }
    public string ProdName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string SpecValues { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }

    /// <summary>库存是否充足</summary>
    public bool StockOk { get; set; }

    /// <summary>库存不足时的提示</summary>
    public string? StockMessage { get; set; }
}

/// <summary>订单预计算结果（后端重算金额 + 库存校验）</summary>
public class OrderPreCalculateResultDto
{
    public List<OrderPreCalculateItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal PayAmount { get; set; }

    /// <summary>全部明细库存是否充足</summary>
    public bool StockOk { get; set; }

    /// <summary>库存不足汇总提示</summary>
    public string? StockMessage { get; set; }
}

/// <summary>订单提交入参（POST /api/order/submit；单事务：库存校验 → 建单 → 余额直扣 → 置已支付）</summary>
public class OrderSubmitDto : IValidatableObject
{
    /// <summary>已保存地址ID（&gt;0 时从 mem_member_address 读取收货信息，并校验归属当前会员）</summary>
    public long AddressId { get; set; }

    // ==================== 内联收货信息（AddressId=0 时必填，作为兜底 / 立即下单） ====================
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceiverProvince { get; set; }
    public string? ReceiverCity { get; set; }
    public string? ReceiverDistrict { get; set; }
    public string? ReceiverAddress { get; set; }

    /// <summary>内联地址是否同时保存为新地址</summary>
    public bool SaveAddress { get; set; }

    // ==================== 金额（后端重算，仅接收优惠与运费） ====================
    [Range(0, double.MaxValue, ErrorMessage = "优惠金额不能为负数")]
    public decimal DiscountAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "运费金额不能为负数")]
    public decimal FreightAmount { get; set; }

    /// <summary>
    /// 商品总价（可选，仅用于前端自检/前置校验）。
    /// ⚠️ 服务端**不信任**此字段，最终校验一律以价格引擎重算的总额为准（见 OrderService.SubmitAsync）。
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>买家备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 支付方式（默认 4-余额支付，保持旧请求兼容）：
    ///   4-余额支付   → 下单即时扣余额，订单直接置「已支付」，扣减库存；
    ///   2-银行转账   → 生成「待付款」订单，不扣余额、不扣库存，等后台录入收款后核销；
    ///   3-货到付款   → 同上（待付款）。
    /// 待付款订单在核销结清其应收时，由 OrderService.MarkPaidFromWriteOffAsync 扣库存并转「已支付」。
    /// </summary>
    public int PayMethod { get; set; } = PayMethods.Balance;

    /// <summary>订单明细（至少一条）</summary>
    [Required(ErrorMessage = "订单明细不能为空")]
    [MinLength(1, ErrorMessage = "订单明细至少需要一条")]
    public List<OrderSubmitItemDto> Items { get; set; } = new();

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in MoneyValidation.ValidateAmounts(
                     DiscountAmount, FreightAmount, TotalAmount, nameof(DiscountAmount), nameof(FreightAmount)))
        {
            yield return error;
        }
    }
}

/// <summary>订单提交结果</summary>
public class OrderSubmitResultDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public decimal PayAmount { get; set; }

    /// <summary>扣款后会员可用余额</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// 下单后的订单状态：1-已支付（余额支付即时扣款）/ 0-待付款（银行转账、货到付款）。
    /// 前端据此展示不同提示与结果页。
    /// </summary>
    public int OrderStatus { get; set; }
}
