using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 订单主表 trx_order（B2B 订货系统，依据 Jsd_order.sql 第 480 行建表语句逐列映射）
///
/// 【状态流转 order_status】（本系统无审核环节，支付即生效）
///   0 待付款 Pending ──支付──&gt; 1 已支付(待发货) Paid ──发货──&gt; 2 已发货(待收货) Shipped ──确认收货──&gt; 3 已完成 Completed
///   0 待付款 Pending ──关闭──&gt; 4 已关闭 Closed
///
/// 【库存联动】
///   - 状态 → 已支付(1)：扣减各 SKU 库存（prod_sku.stock），写变动日志 change_type=4（订单扣减）
///   - 状态 → 已完成(3)：增加各 SKU 销量（prod_sku.sales_count）
///   注意：库存只在【支付】时扣一次，已完成只加销量不再动库存，避免重复扣减。
///
/// 【金额口径】pay_amount(实付) = total_amount(明细小计之和) - discount_amount(优惠) + freight_amount(运费)
/// 【逻辑删除】is_deleted=1 为已删除，所有查询默认过滤 is_deleted=0。
/// </summary>
[Table("trx_order")]
public class TrxOrder
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>订单号（唯一，格式：ORD + yyyyMMddHHmmss + 4位随机数，后端自动生成）</summary>
    [Required]
    [StringLength(64)]
    [Column("order_no")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>买家ID（关联 sys_user.id，默认 0 表示未关联）</summary>
    [Column("buyer_id")]
    public long BuyerId { get; set; }

    // ==================== 收货信息 ====================

    /// <summary>收货人姓名</summary>
    [StringLength(100)]
    [Column("receiver_name")]
    public string ReceiverName { get; set; } = string.Empty;

    /// <summary>收货人联系电话</summary>
    [StringLength(20)]
    [Column("receiver_phone")]
    public string ReceiverPhone { get; set; } = string.Empty;

    /// <summary>收货省份</summary>
    [StringLength(50)]
    [Column("receiver_province")]
    public string ReceiverProvince { get; set; } = string.Empty;

    /// <summary>收货城市</summary>
    [StringLength(50)]
    [Column("receiver_city")]
    public string ReceiverCity { get; set; } = string.Empty;

    /// <summary>收货区县</summary>
    [StringLength(50)]
    [Column("receiver_district")]
    public string ReceiverDistrict { get; set; } = string.Empty;

    /// <summary>收货详细地址</summary>
    [StringLength(500)]
    [Column("receiver_address")]
    public string ReceiverAddress { get; set; } = string.Empty;

    // ==================== 金额 ====================

    /// <summary>订单总金额（所有明细小计之和）</summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>优惠金额</summary>
    [Column("discount_amount", TypeName = "decimal(12,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>运费金额</summary>
    [Column("freight_amount", TypeName = "decimal(12,2)")]
    public decimal FreightAmount { get; set; }

    /// <summary>实付金额 = 总金额 - 优惠 + 运费</summary>
    [Column("pay_amount", TypeName = "decimal(12,2)")]
    public decimal PayAmount { get; set; }

    // ==================== 状态与支付 ====================

    /// <summary>订单状态：0-待付款 1-已支付(待发货) 2-已发货(待收货) 3-已完成 4-已关闭</summary>
    [Column("order_status")]
    public int OrderStatus { get; set; }

    /// <summary>支付方式：0-未支付 1-在线支付 2-银行转账 3-货到付款</summary>
    [Column("pay_method")]
    public int PayMethod { get; set; }

    /// <summary>支付时间（支付成功后写入）</summary>
    [Column("pay_time")]
    public DateTime? PayTime { get; set; }

    // ==================== 物流 ====================

    /// <summary>物流公司（发货时填写）</summary>
    [StringLength(100)]
    [Column("ship_company")]
    public string ShipCompany { get; set; } = string.Empty;

    /// <summary>物流单号（发货时填写）</summary>
    [StringLength(100)]
    [Column("ship_no")]
    public string ShipNo { get; set; } = string.Empty;

    // ==================== 备注 / 关闭 / 完成 ====================

    /// <summary>买家备注（下单时买家填写）</summary>
    [StringLength(500)]
    [Column("buyer_remark")]
    public string BuyerRemark { get; set; } = string.Empty;

    /// <summary>商家备注（后台内部可见；本模块的"修改备注"接口修改的就是这个字段）</summary>
    [StringLength(500)]
    [Column("merchant_remark")]
    public string MerchantRemark { get; set; } = string.Empty;

    /// <summary>
    /// 是否信用订单：0-否（正常支付） 1-是（走应收流程）
    /// 【财务结算模块联动】本订单【发货】且 is_credit=1 时，
    /// ReceivableService.SyncFromOrderAsync 会自动在 trx_receivable 生成应收账款记录。
    /// 普通订单（0）发货后不产生应收。
    /// </summary>
    [Column("is_credit")]
    public int IsCredit { get; set; }

    /// <summary>关闭原因（超时未付 / 用户取消等）</summary>
    [StringLength(200)]
    [Column("close_reason")]
    public string CloseReason { get; set; } = string.Empty;

    /// <summary>关闭时间</summary>
    [Column("close_time")]
    public DateTime? CloseTime { get; set; }

    /// <summary>完成时间（确认收货后置为已完成时写入）</summary>
    [Column("complete_time")]
    public DateTime? CompleteTime { get; set; }

    // ==================== 时间戳 / 逻辑删除 ====================

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>逻辑删除：0-正常 1-已删除</summary>
    [Column("is_deleted")]
    public int IsDeleted { get; set; }

    /// <summary>订单明细集合</summary>
    public virtual List<TrxOrderItem> Items { get; set; } = new();
}
