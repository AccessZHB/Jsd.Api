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
public class OrderPreCalculateDto
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
public class OrderSubmitDto
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

    /// <summary>买家备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>订单明细（至少一条）</summary>
    [Required(ErrorMessage = "订单明细不能为空")]
    [MinLength(1, ErrorMessage = "订单明细至少需要一条")]
    public List<OrderSubmitItemDto> Items { get; set; } = new();
}

/// <summary>订单提交结果</summary>
public class OrderSubmitResultDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public decimal PayAmount { get; set; }

    /// <summary>扣款后会员可用余额</summary>
    public decimal BalanceAfter { get; set; }
}
