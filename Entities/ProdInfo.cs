using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品主表 prod_info（SPU：标准产品单元）
/// 依据 Jsd_order.sql 建表语句逐列精确映射。
/// 重要：价格/划线价/库存等销属字段全部在 prod_sku（SKU）上，SPU 不存价格库存；
/// 数据库也没有 description 列（商品描述用 subtitle/detail）。
/// </summary>
[Table("prod_info")]
public class ProdInfo
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>分类ID（数据库列名 prod_category_id，非 category_id；NOT NULL，关联 prod_category.id）</summary>
    [Column("prod_category_id")]
    public long ProdCategoryId { get; set; }

    /// <summary>商品名称（prod_info_name，VARCHAR(100)）</summary>
    [Required]
    [StringLength(100)]
    [Column("prod_info_name")]
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>商品副标题/卖点描述</summary>
    [StringLength(200)]
    [Column("subtitle")]
    public string? Subtitle { get; set; }

    /// <summary>品牌</summary>
    [StringLength(50)]
    [Column("brand")]
    public string? Brand { get; set; }

    /// <summary>商品编码</summary>
    [StringLength(50)]
    [Column("prod_info_code")]
    public string? ProdInfoCode { get; set; }

    /// <summary>商品主图URL</summary>
    [StringLength(255)]
    [Column("main_image")]
    public string? MainImage { get; set; }

    /// <summary>商品成本价（SPU 级参考成本；售价在 SKU 上）</summary>
    [Column("cost_price", TypeName = "decimal(12,2)")]
    public decimal? CostPrice { get; set; }

    /// <summary>计量单位（片/副/台/盒等）</summary>
    [StringLength(10)]
    [Column("unit")]
    public string? Unit { get; set; }

    /// <summary>最小起订量（下单数量不能小于该值）</summary>
    [Column("min_order_qty")]
    public int? MinOrderQty { get; set; } = 1;

    /// <summary>是否需要配镜参数：1-是 0-否</summary>
    [Column("is_prescription")]
    public int IsPrescription { get; set; }

    /// <summary>是否支持代加工：1-是 0-否</summary>
    [Column("is_process")]
    public int IsProcess { get; set; }

    /// <summary>是否支持单片下单：1-是 0-否（仅镜片）</summary>
    [Column("is_single_piece")]
    public int IsSinglePiece { get; set; }

    /// <summary>供应商ID（可空 = 无供应商，关联 sup_supplier.id）</summary>
    [Column("supplier_id")]
    public long? SupplierId { get; set; }

    /// <summary>销量</summary>
    [Column("sales_count")]
    public int? SalesCount { get; set; }

    /// <summary>图文详情（富文本/HTML，TEXT 大字段）</summary>
    [Column("detail")]
    public string? Detail { get; set; }

    /// <summary>状态：1-上架 0-下架</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>排序号（数据库列名 sort，非 sort_order）</summary>
    [Column("sort")]
    public int? Sort { get; set; }

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }

    /// <summary>所属分类（导航属性，用于 Include 查分类名称）</summary>
    [ForeignKey(nameof(ProdCategoryId))]
    public virtual ProdCategory? Category { get; set; }

    /// <summary>所属供应商（导航属性，用于 Include 查供应商名称）</summary>
    [ForeignKey(nameof(SupplierId))]
    public virtual SupSupplier? Supplier { get; set; }
}
