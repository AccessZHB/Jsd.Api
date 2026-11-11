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

        // 【安全加固】扩展名可被随意改名伪造（把 .aspx 改成 .jpg），因此必须再校验文件头魔数。
        // 只接受真实的图片二进制，拒绝"改了后缀的脚本/HTML/SVG"（SVG 可内嵌 <script>，未列入白名单）。
        if (!await HasValidImageHeaderAsync(file, ext))
        {
            return ApiResponse<object>.Fail("文件内容不是有效的图片（文件头校验失败）");
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

    /// <summary>
    /// 校验文件头魔数是否与扩展名匹配。
    /// 支持：JPEG(FF D8 FF) / PNG(89 50 4E 47 0D 0A 1A 0A) / GIF(47 49 46 38)
    ///      / BMP(42 4D) / WEBP(RIFF....WEBP)。
    /// </summary>
    private static async Task<bool> HasValidImageHeaderAsync(IFormFile file, string ext)
    {
        var header = new byte[12];
        int read;

        await using (var probe = file.OpenReadStream())
        {
            read = await probe.ReadAsync(header.AsMemory(0, header.Length));
        }

        if (read < 4)
        {
            return false;
        }

        var h = header.AsSpan(0, read);

        return ext switch
        {
            ".jpg" or ".jpeg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                                  && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
            ".gif" => h.Length >= 4 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38,
            ".bmp" => h.Length >= 2 && h[0] == 0x42 && h[1] == 0x4D,
            ".webp" => h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                                     && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
            _ => false
        };
    }
}
