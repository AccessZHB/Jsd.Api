using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品分类响应 DTO（列表 / 详情通用，扁平结构）
/// </summary>
public class CategoryDto
{
    public long Id { get; set; }

    /// <summary>父分类ID（0=顶级）</summary>
    public long ParentId { get; set; }

    /// <summary>分类名称</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>分类类型：1-镜片 2-镜架 3-营养品 4-补光设备 5-视觉训练设备 6-远像设备</summary>
    public int? CategoryType { get; set; }

    /// <summary>分类图标URL</summary>
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 商品分类树节点 DTO（GET /tree 返回，递归结构）
/// </summary>
public class CategoryTreeNodeDto
{
    public long Id { get; set; }

    /// <summary>父分类ID（0=顶级）</summary>
    public long ParentId { get; set; }

    /// <summary>分类名称</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>分类类型</summary>
    public int? CategoryType { get; set; }

    /// <summary>分类图标URL</summary>
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; }

    /// <summary>子分类（递归树形结构）</summary>
    public List<CategoryTreeNodeDto> Children { get; set; } = new();
}

/// <summary>
/// 新增分类请求 DTO
/// </summary>
public class CategoryCreateDto
{
    /// <summary>分类名称</summary>
    [Required(ErrorMessage = "分类名称不能为空")]
    [StringLength(50, ErrorMessage = "分类名称最长50个字符")]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>父分类ID（0=顶级分类）</summary>
    [Range(0, long.MaxValue, ErrorMessage = "父分类ID不合法")]
    public long ParentId { get; set; }

    /// <summary>分类类型：1-镜片 2-镜架 3-营养品 4-补光设备 5-视觉训练设备 6-远像设备</summary>
    public int? CategoryType { get; set; }

    /// <summary>分类图标URL</summary>
    [StringLength(255)]
    public string? Icon { get; set; }

    /// <summary>排序号（越小越靠前）</summary>
    public int? Sort { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 修改分类请求 DTO
/// </summary>
public class CategoryUpdateDto
{
    /// <summary>分类ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "分类ID不合法")]
    public long Id { get; set; }

    /// <summary>分类名称</summary>
    [Required(ErrorMessage = "分类名称不能为空")]
    [StringLength(50, ErrorMessage = "分类名称最长50个字符")]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>父分类ID（0=顶级分类）</summary>
    [Range(0, long.MaxValue, ErrorMessage = "父分类ID不合法")]
    public long ParentId { get; set; }

    /// <summary>分类类型</summary>
    public int? CategoryType { get; set; }

    /// <summary>分类图标URL</summary>
    [StringLength(255)]
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    public int? Sort { get; set; }

    /// <summary>状态：1-启用 0-停用</summary>
    public int Status { get; set; } = 1;
}
