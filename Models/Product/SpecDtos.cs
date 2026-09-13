using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Product;

/// <summary>
/// 规格项 DTO。
/// 既用于查询返回，也作为新增/修改商品时的嵌套结构（Values 携带该规格项下的全部规格值）。
/// </summary>
public class SpecItemDto
{
    /// <summary>规格项ID（新增时传 0 即可，由后端生成）</summary>
    public long Id { get; set; }

    /// <summary>商品ID（prod_info_id）</summary>
    public long ProdInfoId { get; set; }

    /// <summary>规格项名称（如折射率、膜层、颜色）</summary>
    [Required(ErrorMessage = "规格项名称不能为空")]
    [StringLength(50, ErrorMessage = "规格项名称最长50个字符")]
    public string SpecName { get; set; } = string.Empty;

    /// <summary>规格类型：1-单选 2-输入</summary>
    public int SpecType { get; set; } = 1;

    /// <summary>是否必填：1-是 0-否</summary>
    public int IsRequired { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>该规格项下的规格值集合（spec_type=2 输入型时可为空）</summary>
    public List<SpecValueDto> Values { get; set; } = new();
}

/// <summary>
/// 规格值 DTO
/// </summary>
public class SpecValueDto
{
    /// <summary>规格值ID（新增时传 0 即可，由后端生成）</summary>
    public long Id { get; set; }

    /// <summary>规格项ID（数据库列名 spec_id）</summary>
    public long SpecId { get; set; }

    /// <summary>商品ID（冗余字段，可空；随 SPU 保存时由后端填充）</summary>
    public long? ProdInfoId { get; set; }

    /// <summary>规格值（如 1.60、防蓝光膜、C1）</summary>
    [Required(ErrorMessage = "规格值不能为空")]
    [StringLength(50, ErrorMessage = "规格值最长50个字符")]
    public string SpecValue { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}

/// <summary>
/// 新增规格项请求 DTO（独立维护接口用）
/// </summary>
public class SpecItemCreateDto
{
    /// <summary>商品ID（prod_info_id）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "商品ID不合法")]
    public long ProdInfoId { get; set; }

    /// <summary>规格项名称</summary>
    [Required(ErrorMessage = "规格项名称不能为空")]
    [StringLength(50, ErrorMessage = "规格项名称最长50个字符")]
    public string SpecName { get; set; } = string.Empty;

    /// <summary>规格类型：1-单选 2-输入</summary>
    public int SpecType { get; set; } = 1;

    /// <summary>是否必填：1-是 0-否</summary>
    public int IsRequired { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}

/// <summary>
/// 修改规格项请求 DTO（独立维护接口用；商品归属不允许修改）
/// </summary>
public class SpecItemUpdateDto
{
    /// <summary>规格项ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "规格项ID不合法")]
    public long Id { get; set; }

    /// <summary>规格项名称</summary>
    [Required(ErrorMessage = "规格项名称不能为空")]
    [StringLength(50, ErrorMessage = "规格项名称最长50个字符")]
    public string SpecName { get; set; } = string.Empty;

    /// <summary>规格类型：1-单选 2-输入</summary>
    public int SpecType { get; set; } = 1;

    /// <summary>是否必填：1-是 0-否</summary>
    public int IsRequired { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}

/// <summary>
/// 新增规格值请求 DTO（独立维护接口用）
/// </summary>
public class SpecValueCreateDto
{
    /// <summary>规格项ID（spec_id）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "规格项ID不合法")]
    public long SpecId { get; set; }

    /// <summary>商品ID（冗余字段，可空）</summary>
    public long? ProdInfoId { get; set; }

    /// <summary>规格值</summary>
    [Required(ErrorMessage = "规格值不能为空")]
    [StringLength(50, ErrorMessage = "规格值最长50个字符")]
    public string SpecValue { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}

/// <summary>
/// 修改规格值请求 DTO（独立维护接口用；规格项归属不允许修改）
/// </summary>
public class SpecValueUpdateDto
{
    /// <summary>规格值ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "规格值ID不合法")]
    public long Id { get; set; }

    /// <summary>规格值</summary>
    [Required(ErrorMessage = "规格值不能为空")]
    [StringLength(50, ErrorMessage = "规格值最长50个字符")]
    public string SpecValue { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}
