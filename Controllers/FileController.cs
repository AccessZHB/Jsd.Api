using Jsd.Api.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 文件上传接口（商品图片/主图/轮播图/详情图通用）
/// 上传后保存到 wwwroot/upload/yyyyMM/ 目录，通过静态文件中间件访问
/// </summary>
[ApiController]
[Route("api/file")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    /// <summary>允许的图片扩展名</summary>
    private static readonly string[] AllowedExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    /// <summary>最大文件大小：10MB</summary>
    private const long MaxSize = 10 * 1024 * 1024;

    public FileController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// 上传图片
    /// 示例：POST /api/file/upload  (multipart/form-data, 字段名 file)
    /// 返回：{ "url": "/upload/202609/xxxxxxxx.jpg" }
    /// </summary>
    [HttpPost("upload")]
    public async Task<ApiResponse<object>> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResponse<object>.Fail("请选择要上传的文件");
        }

        if (file.Length > MaxSize)
        {
            return ApiResponse<object>.Fail("文件大小不能超过 10MB");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExts.Contains(ext))
        {
            return ApiResponse<object>.Fail("仅支持 jpg/jpeg/png/gif/webp/bmp 格式的图片");
        }

        // 按月份分目录：wwwroot/upload/202609/
        var monthDir = DateTime.Now.ToString("yyyyMM");
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var saveDir = Path.Combine(webRoot, "upload", monthDir);
        Directory.CreateDirectory(saveDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var savePath = Path.Combine(saveDir, fileName);

        await using (var fs = new FileStream(savePath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        // 返回相对 URL（生产环境由 Nginx/静态文件托管；开发环境由 Vite 代理 /upload）
        var url = $"/upload/{monthDir}/{fileName}";
        return ApiResponse<object>.Success(new { url }, "上传成功");
    }
}
