using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Order;

/// <summary>
/// 创建订单入参（POST /api/order）
///
/// 金额说明：后端不信任前端传来的金额，一律以【SKU 当前零售价 × 数量】重算：
///   total_amount = Σ(unit_price × quantity)
///   pay_amount   = total_amount - discount_amount + freight_amount
/// 商品名称 / SKU名称 / 规格 / 图片 / 单价由后端从 prod_info、prod_sku 读取后写入明细快照。
/// </summary>
public class OrderCreateDto
{
    /// <summary>买家ID（关联 sys_user.id）</summary>
    public long BuyerId { get; set; }

    // ==================== 收货信息 ====================

    /// <summary>收货人姓名</summary>
    [Required(ErrorMessage = "收货人姓名不能为空")]
    [StringLength(100, ErrorMessage = "收货人姓名不能超过100个字符")]
    public string ReceiverName { get; set; } = string.Empty;

    /// <summary>收货人联系电话</summary>
    [Required(ErrorMessage = "收货人联系电话不能为空")]
    [StringLength(20, ErrorMessage = "联系电话不能超过20个字符")]
    public string ReceiverPhone { get; set; } = string.Empty;

    /// <summary>收货省份</summary>
    [StringLength(50)]
    public string? ReceiverProvince { get; set; }

    /// <summary>收货城市</summary>
    [StringLength(50)]
    public string? ReceiverCity { get; set; }

    /// <summary>收货区县</summary>
    [StringLength(50)]
    public string? ReceiverDistrict { get; set; }

    /// <summary>收货详细地址</summary>
    [Required(ErrorMessage = "收货详细地址不能为空")]
    [StringLength(500, ErrorMessage = "收货地址不能超过500个字符")]
    public string ReceiverAddress { get; set; } = string.Empty;

    // ==================== 金额（后端重算，仅接收优惠与运费） ====================

    /// <summary>优惠金额</summary>
    [Range(0, double.MaxValue, ErrorMessage = "优惠金额不能为负数")]
    public decimal DiscountAmount { get; set; }

    /// <summary>运费金额</summary>
    [Range(0, double.MaxValue, ErrorMessage = "运费金额不能为负数")]
    public decimal FreightAmount { get; set; }

    /// <summary>支付方式：0-未支付 1-在线支付 2-银行转账 3-货到付款（下单时默认未支付）</summary>
    public int PayMethod { get; set; } = PayMethods.Unpaid;

    /// <summary>买家备注</summary>
    [StringLength(500)]
    public string? BuyerRemark { get; set; }

    /// <summary>商家备注</summary>
    [StringLength(500)]
    public string? MerchantRemark { get; set; }

    /// <summary>订单明细（至少一条）</summary>
    [Required(ErrorMessage = "订单明细不能为空")]
    [MinLength(1, ErrorMessage = "订单明细至少需要一条")]
    public List<OrderItemInputDto> Items { get; set; } = new();
}

/// <summary>创建订单时的明细入参（只需传商品、SKU、数量，其余由后端快照）</summary>
public class OrderItemInputDto
{
    /// <summary>商品ID（prod_info_id）</summary>
    [Required(ErrorMessage = "商品ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "商品ID非法")]
    public long ProdInfoId { get; set; }

    /// <summary>SKU ID（sku_id）</summary>
    [Required(ErrorMessage = "SKU ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "SKU ID非法")]
    public long SkuId { get; set; }

    /// <summary>购买数量</summary>
    [Range(1, int.MaxValue, ErrorMessage = "购买数量必须大于0")]
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// 修改订单备注入参（PUT /api/order/{id}/remark）
/// 【状态校验】只有 待付款 Pending(0) 的订单才允许修改（与修改收货地址同规则）；
/// 已支付及之后的订单禁止修改，避免与已发货/已完成状态产生歧义。
/// 修改的是 merchant_remark（商家备注，后台内部可见），不影响 buyer_remark。
/// </summary>
public class OrderRemarkDto
{
    /// <summary>商家备注</summary>
    [StringLength(500, ErrorMessage = "备注不能超过500个字符")]
    public string MerchantRemark { get; set; } = string.Empty;
}

/// <summary>
/// 手动发货入参（PUT /api/order/{id}/ship）
/// 【状态校验】只有 已支付 Paid(1) 的订单才允许发货（待付款未扣库存、已发货不可重复发货）。
/// </summary>
public class OrderShipDto
{
    /// <summary>物流公司</summary>
    [Required(ErrorMessage = "物流公司不能为空")]
    [StringLength(100, ErrorMessage = "物流公司不能超过100个字符")]
    public string ShipCompany { get; set; } = string.Empty;

    /// <summary>物流单号</summary>
    [Required(ErrorMessage = "物流单号不能为空")]
    [StringLength(100, ErrorMessage = "物流单号不能超过100个字符")]
    public string ShipNo { get; set; } = string.Empty;
}

/// <summary>
/// 支付入参（PUT /api/order/{id}/pay）—— 支撑"支付即扣库存"的补充接口
/// 【状态校验】只有 待付款 Pending(0) 才能支付；支付成功后扣减各 SKU 库存。
/// </summary>
public class OrderPayDto
{
    /// <summary>支付方式：1-在线支付 2-银行转账 3-货到付款（不能为 0-未支付）</summary>
    [Range(1, 3, ErrorMessage = "支付方式非法（1-在线支付 2-银行转账 3-货到付款）")]
    public int PayMethod { get; set; } = PayMethods.Online;
}

/// <summary>
/// 关闭订单入参（PUT /api/order/{id}/cancel）
/// 【状态校验】只有 待付款 Pending(0) 才能关闭（已支付订单需走退款流程，本模块不处理）。
/// </summary>
public class OrderCloseDto
{
    /// <summary>关闭原因（超时未付 / 用户取消等）</summary>
    [StringLength(200, ErrorMessage = "关闭原因不能超过200个字符")]
    public string? CloseReason { get; set; }
}
