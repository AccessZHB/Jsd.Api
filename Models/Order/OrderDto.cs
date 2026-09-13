namespace Jsd.Api.Models.Order;

// ============================================================
// 常量：订单状态 / 支付方式
// ============================================================

/// <summary>
/// 订单状态（对应 trx_order.order_status，TINYINT）
///
/// 本系统【无审核环节】，状态流转如下（只允许沿箭头方向流转，不允许回退）：
///   0 待付款 Pending
///      ├─ 支付 ──&gt; 1 已支付(待发货) Paid      ← 此步【扣减 SKU 库存】
///      └─ 关闭 ──&gt; 4 已关闭 Closed
///   1 已支付(待发货) Paid
///      └─ 发货 ──&gt; 2 已发货(待收货) Shipped
///   2 已发货(待收货) Shipped
///      └─ 确认收货 ──&gt; 3 已完成 Completed      ← 此步【增加 SKU 销量】
///   3 已完成 Completed（终态）
///   4 已关闭 Closed（终态）
///
/// 关键校验：
///   - 只有 Paid(1) 才能执行【发货】；
///   - 只有 Pending(0) 才能修改【收货地址 / 备注】；
///   - 只有 Shipped(2) 才能执行【完成】；
///   - 只有 Pending(0) 才能执行【支付】与【关闭】。
/// </summary>
public static class OrderStatuses
{
    /// <summary>0-待付款（可支付、可关闭、可改地址/备注）</summary>
    public const int Pending = 0;

    /// <summary>1-已支付/待发货（可发货）</summary>
    public const int Paid = 1;

    /// <summary>2-已发货/待收货（可确认完成）</summary>
    public const int Shipped = 2;

    /// <summary>3-已完成（终态）</summary>
    public const int Completed = 3;

    /// <summary>4-已关闭（终态）</summary>
    public const int Closed = 4;

    /// <summary>订单状态 → 中文名（列表 Tag 展示）</summary>
    public static string GetName(int status) => status switch
    {
        Pending => "待付款",
        Paid => "已支付",
        Shipped => "已发货",
        Completed => "已完成",
        Closed => "已关闭",
        _ => "未知"
    };
}

/// <summary>
/// 支付方式（对应 trx_order.pay_method，TINYINT）
/// </summary>
public static class PayMethods
{
    /// <summary>0-未支付</summary>
    public const int Unpaid = 0;

    /// <summary>1-在线支付</summary>
    public const int Online = 1;

    /// <summary>2-银行转账</summary>
    public const int BankTransfer = 2;

    /// <summary>3-货到付款</summary>
    public const int CashOnDelivery = 3;

    /// <summary>支付方式 → 中文名</summary>
    public static string GetName(int payMethod) => payMethod switch
    {
        Unpaid => "未支付",
        Online => "在线支付",
        BankTransfer => "银行转账",
        CashOnDelivery => "货到付款",
        _ => "未知"
    };
}

// ============================================================
// 列表 / 详情 / 明细 DTO
// ============================================================

/// <summary>
/// 订单列表项（主表信息 + 商品总数）
/// ItemCount = 明细行数（商品种类数）；TotalQuantity = 商品件数合计（各明细数量之和）。
/// 两者都由 Service 一次性 group by 统计后回填，避免 N+1 查询。
/// </summary>
public class OrderListDto
{
    /// <summary>主键ID</summary>
    public long Id { get; set; }

    /// <summary>订单号（ORD + yyyyMMddHHmmss + 4位随机数）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>买家ID</summary>
    public long BuyerId { get; set; }

    // ===== 收货信息 =====
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ReceiverProvince { get; set; } = string.Empty;
    public string ReceiverCity { get; set; } = string.Empty;
    public string ReceiverDistrict { get; set; } = string.Empty;
    public string ReceiverAddress { get; set; } = string.Empty;

    // ===== 金额 =====
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal PayAmount { get; set; }

    // ===== 状态 =====
    /// <summary>订单状态：0-待付款 1-已支付 2-已发货 3-已完成 4-已关闭</summary>
    public int OrderStatus { get; set; }

    /// <summary>订单状态中文名（展示字段）</summary>
    public string? OrderStatusName { get; set; }

    /// <summary>支付方式：0-未支付 1-在线支付 2-银行转账 3-货到付款</summary>
    public int PayMethod { get; set; }

    /// <summary>支付方式中文名（展示字段）</summary>
    public string? PayMethodName { get; set; }

    public DateTime? PayTime { get; set; }

    // ===== 物流 =====
    public string ShipCompany { get; set; } = string.Empty;
    public string ShipNo { get; set; } = string.Empty;

    // ===== 备注 / 关闭 / 完成 =====
    public string BuyerRemark { get; set; } = string.Empty;
    public string MerchantRemark { get; set; } = string.Empty;
    public string CloseReason { get; set; } = string.Empty;
    public DateTime? CloseTime { get; set; }
    public DateTime? CompleteTime { get; set; }

    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    /// <summary>商品总数（明细行数 / 商品种类数）</summary>
    public int ItemCount { get; set; }

    /// <summary>商品件数合计（各明细 quantity 之和）</summary>
    public int TotalQuantity { get; set; }
}

/// <summary>订单详情 = 主表信息 + 订单明细列表</summary>
public class OrderDetailDto : OrderListDto
{
    /// <summary>订单明细列表（trx_order_item）</summary>
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// 订单明细分项（trx_order_item）
/// 名称/规格/图片/单价均为下单瞬间的快照，不随商品改动而变化。
/// </summary>
public class OrderItemDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProdInfoId { get; set; }
    public long SkuId { get; set; }

    /// <summary>商品名称（快照）</summary>
    public string ProdName { get; set; } = string.Empty;

    /// <summary>SKU 名称（快照）</summary>
    public string SkuName { get; set; } = string.Empty;

    /// <summary>规格值（快照）</summary>
    public string SpecValues { get; set; } = string.Empty;

    /// <summary>商品图片（快照）</summary>
    public string ProductImage { get; set; } = string.Empty;

    /// <summary>单价（快照）</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; set; }

    /// <summary>小计金额 = 单价 × 数量</summary>
    public decimal SubtotalAmount { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 订单导出结果（GET /api/order/export）
/// 当前为【占位实现】：不生成真实 Excel，只返回提示信息与命中的数据条数；
/// 后续接入 Excel 生成（如 EPPlus / MiniExcel）后，把文件地址放进 ExportUrl 即可，
/// 接口签名保持不变，前端无需改动。
/// </summary>
public class OrderExportDto
{
    /// <summary>导出文件地址（占位阶段为 null）</summary>
    public string? ExportUrl { get; set; }

    /// <summary>提示信息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>按当前筛选条件命中的订单条数</summary>
    public int Total { get; set; }
}
