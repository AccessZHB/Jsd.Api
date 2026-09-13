using System.ComponentModel.DataAnnotations;

namespace Jsd.Api.Models.Product;

/// <summary>
/// 商品图片响应 DTO（prod_image 表，原名 pro_image）
/// </summary>
public class ProdImageDto
{
    public long Id { get; set; }

    /// <summary>关联商品ID</summary>
    public long? ProdInfoId { get; set; }

    /// <summary>图片类型：'1'-主图 '2'-轮播图 '3'-详情图</summary>
    public string? ImageType { get; set; }

    /// <summary>图片URL</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}

/// <summary>
/// 商品图片保存 DTO（嵌套在商品新增/修改请求中，随 SPU 一同提交）
/// </summary>
public class ProdImageSaveDto
{
    /// <summary>图片类型：'1'-主图 '2'-轮播图 '3'-详情图</summary>
    [Required(ErrorMessage = "图片类型不能为空")]
    public string ImageType { get; set; } = "1";

    /// <summary>图片URL（上传接口返回）</summary>
    [Required(ErrorMessage = "图片URL不能为空")]
    [StringLength(512)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int? Sort { get; set; }
}
