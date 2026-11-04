using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 会员购物车表 mem_cart（小程序购物车，服务端持久化，支持跨设备同步）
///
/// 【业务背景 / 为什么要有这张表】
/// 历史上购物车是纯前端 localStorage（jsd_mp_cart_*），带来两个致命问题：
///   1. 不同加工属性的镜片被错误合并：唯一键只含 商品ID+SKU+球镜柱镜，
///      导致「需要加工」与「只发片」撞同一个 key，数量被叠加、加工属性丢失；
///   2. 无法跨设备同步：换手机 / 重装就丢购物车。
/// 本表把购物车落到服务端，并用 cart_key（商品 + SKU + 加工类型 + 验光哈希）做唯一键，
/// 从数据库层面保证「加工 / 不加工」绝不会被合并成一条。
/// </summary>
[Table("mem_cart")]
public class MemCart
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>会员ID（关联 mem_member.id；与后台管理员 sys_user 物理隔离）</summary>
    [Column("member_id")]
    public long MemberId { get; set; }

    /// <summary>商品ID（prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>规格SKU ID（prod_sku.id）</summary>
    [Column("sku_id")]
    public long SkuId { get; set; }

    /// <summary>数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// 是否需要加工：0-不需要加工（只发片） 1-需要加工（邮寄镜架）
    /// 【合并判定的第一要素】0 与 1 永远是两条独立记录，绝不合并数量。
    /// </summary>
    [Column("processing_type")]
    public int ProcessingType { get; set; }

    /// <summary>
    /// 验光数据（JSON 字符串，结构：{ left:{sphere,cylinder,axis,pd,height}, right:{...} }）
    /// 只发片（processing_type=0）时为空。
    /// 注意：本列不参与比较，唯一性比较统一走 prescription_hash（规范化后的 SHA256）。
    /// </summary>
    [Column("prescription_data", TypeName = "longtext")]
    public string? PrescriptionData { get; set; }

    /// <summary>
    /// 验光数据哈希（规范化验光内容后的 SHA256 十六进制；无验光固定为 NORX）
    /// 用于把「球镜/柱镜/轴位/瞳距/瞳高」全量纳入唯一键，避免逐列比较与 JSON 顺序差异问题。
    /// </summary>
    [StringLength(64)]
    [Column("prescription_hash")]
    public string PrescriptionHash { get; set; } = string.Empty;

    /// <summary>
    /// 购物车行唯一键：{memberId}_{prodInfoId}_{skuId}_{processingType}_{prescriptionHash}
    /// 数据库侧建 UNIQUE 索引，是「不同加工属性绝不合并」的最后一道防线。
    /// </summary>
    [StringLength(128)]
    [Column("cart_key")]
    public string CartKey { get; set; } = string.Empty;

    /// <summary>是否勾选（B2B 支持部分勾选结算，存表以便跨端一致）：0-否 1-是</summary>
    [Column("is_selected")]
    public int IsSelected { get; set; } = 1;

    /// <summary>加入时间（数据库默认 CURRENT_TIMESTAMP）</summary>
    [Column("add_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime AddTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
