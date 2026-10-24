using Jsd.Api.Helpers;
using Jsd.Api.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 文件上传接口（商品图片/主图/轮播图/详情图/物流面单通用）
/// 上传后保存到 wwwroot/upload/yyyyMM/ 目录，通过静态文件中间件访问。
/// 保存逻辑见 Helpers/ImageSaver（与 AdminUploadController 共用）。
/// </summary>
[ApiController]
[Route("api/file")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public FileController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// 上传图片
    /// 示例：POST /api/file/upload  (multipart/form-data, 字段名 file)
    /// 返回：{ "url": "/upload/202609/xxxxxxxx.jpg" }
    /// 另提供 /api/file/upload/image 作为图像专用别名。
    /// </summary>
    [HttpPost("upload")]
    [HttpPost("upload/image")]
    public Task<ApiResponse<object>> Upload(IFormFile? file)
    {
        return ImageSaver.SaveAsync(_env, file);
    }
}
