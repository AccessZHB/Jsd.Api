using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 采购入库单主表 pur_inbound（采购管理模块）
///
/// 实现"以采定入"核心逻辑：仓库人员选择一张"审批通过/待入库"(status=2) 且未完全入库的
/// 采购订单来创建入库单，系统自动带出订单明细，实收数量可 ≤ 订单数量（分批到货）。
/// 入库单创建后为"待确认"(0)，确认后(status=1)才真正增加库存并生成应付。
///
/// 【warehouse_id】本系统当前无仓库主数据表（多仓库为待建功能），warehouse_id 仅作引用存储，
/// 不配置物理外键；接入仓库模块后可在 Service 层补充校验。
/// </summary>
[Table("pur_inbound")]
public class PurInbound
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>入库单号（唯一，格式 RK-yyyyMMdd-001，后端自动生成）</summary>
    [Required]
    [StringLength(32)]
    [Column("inbound_no")]
    public string InboundNo { get; set; } = string.Empty;

    /// <summary>关联采购订单ID（pur_order.id，核心关联字段）</summary>
    [Column("order_id")]
    public long OrderId { get; set; }

    /// <summary>仓库ID（引用，当前无仓库主数据表）</summary>
    [Column("warehouse_id")]
    public long WarehouseId { get; set; }

    /// <summary>入库日期（DATE）</summary>
    [Column("inbound_date")]
    public DateTime InboundDate { get; set; }

    /// <summary>入库操作员ID（可空）</summary>
    [Column("operator_id")]
    public long? OperatorId { get; set; }

    /// <summary>状态：0-待确认 1-已入库</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>所属采购订单（查询时 Include，用于带出供应商/单号）</summary>
    public PurOrder? Order { get; set; }

    /// <summary>入库明细集合</summary>
    public List<PurInboundItem> Items { get; set; } = new();
}
