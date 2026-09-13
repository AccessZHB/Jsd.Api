using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 商品SKU明细表 prod_sku（SKU：库存量单位）
/// 一个商品（SPU）可含多个 SKU；规格项/规格值仅用于配置与展示，
/// 真正的价格、库存、条码全部绑定在 SKU 上；sku_code 唯一。
/// </summary>
[Table("prod_sku")]
public class ProdSku
{
    /// <summary>主键ID</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>商品ID（数据库列名 prod_info_id，非 product_id；关联 prod_info.id）</summary>
    [Column("prod_info_id")]
    public long ProdInfoId { get; set; }

    /// <summary>
    /// SKU编码（VARCHAR(50)，UNIQUE，全局唯一）。
    /// 【后端自动生成】规则：固定前缀 SKU + 时间戳 yyyyMMddHHmmss + 4位随机数，
    /// 例：SKU202609082310451234。保存时一律忽略前端传值。
    /// </summary>
    [Required]
    [StringLength(50)]
    [Column("sku_code")]
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>SKU名称</summary>
    [StringLength(100)]
    [Column("sku_name")]
    public string? SkuName { get; set; }

    /// <summary>
    /// 规格值ID组合（VARCHAR(255)，如 "10" 或 "10,15"），对应 prod_spec_value.id。
    /// 由后端从前端提交的规格项数组中提取 spec_value_id 拼接生成；
    /// 展示文本由后端按ID反查（见 ProdSkuDto.SpecValuesText）。
    /// </summary>
    [StringLength(255)]
    [Column("spec_values")]
    public string? SpecValues { get; set; }

    /// <summary>SKU零售价（原 price → retail_price，NOT NULL）</summary>
    [Column("retail_price", TypeName = "decimal(12,2)")]
    public decimal RetailPrice { get; set; }

    /// <summary>SKU销售价（原 original_price → sale_price；划线价/活动价）</summary>
    [Column("sale_price", TypeName = "decimal(12,2)")]
    public decimal? SalePrice { get; set; }

    /// <summary>SKU库存量（NOT NULL）</summary>
    [Column("stock")]
    public int Stock { get; set; }

    /// <summary>SKU销量</summary>
    [Column("sales_count")]
    public int? SalesCount { get; set; }

    /// <summary>SKU图片URL</summary>
    [StringLength(255)]
    [Column("image")]
    public string? Image { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>创建时间（数据库自动生成）</summary>
    [Column("create_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE 自动维护）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
