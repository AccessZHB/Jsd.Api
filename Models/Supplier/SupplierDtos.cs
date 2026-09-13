using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Supplier;

/// <summary>
/// 供应商响应 DTO（列表 / 详情通用）
/// </summary>
public class SupplierDto
{
    public long Id { get; set; }

    /// <summary>供应商名称</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>联系人</summary>
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    public string? ContactPhone { get; set; }

    /// <summary>合作状态：1-启用 0-禁用</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 新增供应商请求 DTO
/// </summary>
public class SupplierCreateDto
{
    /// <summary>供应商名称</summary>
    [Required(ErrorMessage = "供应商名称不能为空")]
    [StringLength(200, ErrorMessage = "供应商名称最长200个字符")]
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>联系人</summary>
    [StringLength(100)]
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    [StringLength(50)]
    public string? ContactPhone { get; set; }

    /// <summary>合作状态：1-启用 0-禁用</summary>
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 修改供应商请求 DTO
/// </summary>
public class SupplierUpdateDto
{
    /// <summary>供应商ID（必传）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "供应商ID不合法")]
    public long Id { get; set; }

    /// <summary>供应商名称</summary>
    [Required(ErrorMessage = "供应商名称不能为空")]
    [StringLength(200, ErrorMessage = "供应商名称最长200个字符")]
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>联系人</summary>
    [StringLength(100)]
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    [StringLength(50)]
    public string? ContactPhone { get; set; }

    /// <summary>合作状态：1-启用 0-禁用</summary>
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(500)]
    public string? Remark { get; set; }
}
