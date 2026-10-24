using Jsd.Api.Models.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jsd.Api.Helpers;

/// <summary>
/// 图片上传共享逻辑：保存到 wwwroot/upload/yyyyMM/ 目录，返回相对访问路径。
/// FileController（/api/file/upload、/api/file/upload/image）与
/// AdminUploadController（/api/admin/upload/image）共用，避免逻辑分散。
/// </summary>
public static class ImageSaver
{
    /// <summary>允许的图片扩展名</summary>
    private static readonly string[] AllowedExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    /// <summary>最大文件大小：10MB</summary>
    private const long MaxSize = 10 * 1024 * 1024;

    /// <summary>
    /// 保存上传的图片文件。
    /// 成功返回 ApiResponse.Success(new { url = "/upload/yyyyMM/xxx.jpg" }, "上传成功")；
    /// 校验失败返回 ApiResponse.Fail(...)。
    /// </summary>
    public static async Task<ApiResponse<object>> SaveAsync(IWebHostEnvironment env, IFormFile? file)
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

        // 按月份分目录：wwwroot/upload/202610/
        var monthDir = DateTime.Now.ToString("yyyyMM");
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
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
