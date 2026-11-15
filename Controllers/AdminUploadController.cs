using Jsd.Api.Attributes;
using Jsd.Api.Helpers;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.File;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 后台通用图片上传接口（管理员专用）。
/// 路由：POST /api/admin/upload/image  (multipart/form-data, 字段名 file)
/// 与 /api/file/upload 共用 Helpers/ImageSaver 保存逻辑，返回一致的相对路径。
/// </summary>
[ApiController]
[Route("api/admin/upload")]
[Authorize]
public class AdminUploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public AdminUploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// 上传图片（物流面单/商品图等通用）
    /// 返回：{ "url": "/upload/yyyyMM/xxxxxxxx.jpg" }
    /// </summary>
    [HttpPost("image")]
    [Permission(FilePermissions.Upload)]    // file:upload（与 /api/file/upload 共用）
    public Task<ApiResponse<object>> Image(IFormFile? file)
    {
        return ImageSaver.SaveAsync(_env, file);
    }
}
