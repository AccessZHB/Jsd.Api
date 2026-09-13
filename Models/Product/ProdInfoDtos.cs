using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品（SPU）响应 DTO（分页列表用，不含大字段 Detail）
/// </summary>
public class ProdInfoDto
{
    public long Id { get; set; }

    /// <summary>商品名称（prod_info_name）</summary>
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>分类ID（prod_category_id）</summary>
    public long ProdCategoryId { get; set; }

    /// <summary>分类名称（Include 导航属性映射）</summary>
    public string? CategoryName { get; set; }

    /// <summary>供应商ID（可空）</summary>
    public long? SupplierId { get; set; }

    /// <summary>供应商名称（Include 导航属性映射）</summary>
    public string? SupplierName { get; set; }

    /// <summary>商品编码</summary>
    public string? ProdInfoCode { get; set; }

    /// <summary>品牌</summary>
    public string? Brand { get; set; }

    /// <summary>副标题/卖点</summary>
    public string? Subtitle { get; set; }

    /// <summary>主图URL</summary>
    public string? MainImage { get; set; }

    /// <summary>成本价（SPU 级参考）</summary>
    public decimal? CostPrice { get; set; }

    /// <summary>计量单位</summary>
    public string? Unit { get; set; }

    /// <summary>最小起订量</summary>
    public int? MinOrderQty { get; set; }

    /// <summary>是否需要配镜参数：1-是 0-否</summary>
    public int IsPrescription { get; set; }

    /// <summary>是否支持代加工：1-是 0-否</summary>
    public int IsProcess { get; set; }

    /// <summary>是否支持单片下单：1-是 0-否</summary>
    public int IsSinglePiece { get; set; }

    /// <summary>销量</summary>
    public int? SalesCount { get; set; }

    /// <summary>状态：1-上架 0-下架</summary>
    public int Status { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>SKU 最低售价（列表"价格区间"展示；无 SKU 时为 null）</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>SKU 最高售价（列表"价格区间"展示）</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>库存总量（所有启用 SKU 的 stock 之和）</summary>
    public int TotalStock { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 商品详情 DTO（GET /{id}/detail 返回：SPU + 图片 + 全部规格项（含规格值） + 全部 SKU）
/// </summary>
public class ProdInfoDetailDto : ProdInfoDto
{
    /// <summary>图文详情（富文本/HTML）</summary>
    public string? Detail { get; set; }

    /// <summary>商品图片列表（按 image_type 区分主图/轮播图/详情图）</summary>
    public List<ProdImageDto> Images { get; set; } = new();

    /// <summary>该商品的规格项列表（含每项下的规格值）</summary>
    public List<SpecItemDto> SpecItems { get; set; } = new();

    /// <summary>该商品的 SKU 列表（价格/库存在此层）</summary>
    public List<ProdSkuDto> Skus { get; set; } = new();
}

/// <summary>
/// 新增商品请求 DTO
/// 除 SPU 基础字段外，携带规格配置 SpecItems 与前端组合好的 SKU 列表 Skus，
/// 后端在同一事务中保存 SPU、规格、SKU 三部分数据。
/// </summary>
public class ProdInfoCreateDto
{
    /// <summary>分类ID（prod_category_id，NOT NULL 必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品分类")]
    public long ProdCategoryId { get; set; }

    /// <summary>商品名称</summary>
    [Required(ErrorMessage = "商品名称不能为空")]
    [StringLength(100, ErrorMessage = "商品名称最长100个字符")]
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>副标题/卖点</summary>
    [StringLength(200)]
    public string? Subtitle { get; set; }

    /// <summary>品牌</summary>
    [StringLength(50)]
    public string? Brand { get; set; }

    /// <summary>商品编码</summary>
    [StringLength(50)]
    public string? ProdInfoCode { get; set; }

    /// <summary>主图URL</summary>
    [StringLength(255)]
    public string? MainImage { get; set; }

    /// <summary>成本价（SPU 级参考）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "成本价不能为负数且不能超过上限")]
    public decimal? CostPrice { get; set; }

    /// <summary>计量单位（片/副/台/盒等）</summary>
    [StringLength(10)]
    public string? Unit { get; set; }

    /// <summary>最小起订量（下单数量不能小于该值）</summary>
    [Range(1, int.MaxValue, ErrorMessage = "最小起订量至少为1")]
    public int? MinOrderQty { get; set; } = 1;

    /// <summary>是否需要配镜参数：1-是 0-否</summary>
    public int IsPrescription { get; set; }

    /// <summary>是否支持代加工：1-是 0-否</summary>
    public int IsProcess { get; set; }

    /// <summary>是否支持单片下单：1-是 0-否</summary>
    public int IsSinglePiece { get; set; }

    /// <summary>供应商ID（可空 = 无供应商）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "供应商ID不合法")]
    public long? SupplierId { get; set; }

    /// <summary>图文详情（富文本/HTML）</summary>
    public string? Detail { get; set; }

    /// <summary>状态：1-上架 0-下架</summary>
    public int Status { get; set; } = 1;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>规格配置（规格项+规格值，随 SPU 一同保存）</summary>
    public List<SpecItemDto> SpecItems { get; set; } = new();

    /// <summary>前端组合好的 SKU 列表（价格/库存/编码，随 SPU 一同保存）</summary>
    public List<ProdSkuCreateDto> Skus { get; set; } = new();

    /// <summary>商品图片列表（主图/轮播图/详情图，随 SPU 一同保存）</summary>
    public List<ProdImageSaveDto> Images { get; set; } = new();
}

/// <summary>
/// 修改商品请求 DTO
/// 注意（业务约定）：修改规格信息不自动改动已生成 SKU；改价格/库存直接走 SKU 修改。
/// </summary>
public class ProdInfoUpdateDto
{
    /// <summary>商品ID（由路由 {id} 填充并在 Controller 中赋值，请求体无需传递）</summary>
    public long Id { get; set; }

    /// <summary>分类ID（NOT NULL 必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择商品分类")]
    public long ProdCategoryId { get; set; }

    /// <summary>商品名称</summary>
    [Required(ErrorMessage = "商品名称不能为空")]
    [StringLength(100, ErrorMessage = "商品名称最长100个字符")]
    public string ProdInfoName { get; set; } = string.Empty;

    /// <summary>副标题/卖点</summary>
    [StringLength(200)]
    public string? Subtitle { get; set; }

    /// <summary>品牌</summary>
    [StringLength(50)]
    public string? Brand { get; set; }

    /// <summary>商品编码</summary>
    [StringLength(50)]
    public string? ProdInfoCode { get; set; }

    /// <summary>主图URL</summary>
    [StringLength(255)]
    public string? MainImage { get; set; }

    /// <summary>成本价（SPU 级参考）</summary>
    [Range(0, 9999999999.99, ErrorMessage = "成本价不能为负数且不能超过上限")]
    public decimal? CostPrice { get; set; }

    /// <summary>计量单位</summary>
    [StringLength(10)]
    public string? Unit { get; set; }

    /// <summary>最小起订量</summary>
    [Range(1, int.MaxValue, ErrorMessage = "最小起订量至少为1")]
    public int? MinOrderQty { get; set; } = 1;

    /// <summary>是否需要配镜参数：1-是 0-否</summary>
    public int IsPrescription { get; set; }

    /// <summary>是否支持代加工：1-是 0-否</summary>
    public int IsProcess { get; set; }

    /// <summary>是否支持单片下单：1-是 0-否</summary>
    public int IsSinglePiece { get; set; }

    /// <summary>供应商ID（可空）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "供应商ID不合法")]
    public long? SupplierId { get; set; }

    /// <summary>图文详情（富文本/HTML）</summary>
    public string? Detail { get; set; }

    /// <summary>状态：1-上架 0-下架</summary>
    public int Status { get; set; } = 1;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>规格配置（修改时全量替换该商品的规格项/规格值）</summary>
    public List<SpecItemDto> SpecItems { get; set; } = new();

    /// <summary>SKU 列表（修改时全量替换该商品的 SKU）</summary>
    public List<ProdSkuCreateDto> Skus { get; set; } = new();

    /// <summary>商品图片列表（修改时全量替换该商品的图片）</summary>
    public List<ProdImageSaveDto> Images { get; set; } = new();
}
