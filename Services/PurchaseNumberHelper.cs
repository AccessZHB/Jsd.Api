using Jsd.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 采购模块单号生成工具。
/// 规则：前缀 + yyyyMMdd + 序号（如 PO-20260919-001），序号按当天递增并自动避让冲突。
/// 数据库唯一索引是最后一道防线；本方法在生成时先查重再重试（规范 6.6）。
/// </summary>
public static class PurchaseNumberHelper
{
    /// <summary>
    /// 生成不重复的单号。
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="prefix">单号前缀（PO / RK / AP）</param>
    /// <param name="existsAsync">判断某单号是否已存在的异步函数</param>
    public static async Task<string> GenerateAsync(
        AppDbContext db, string prefix, Func<string, Task<bool>> existsAsync)
    {
        var datePart = DateTime.Now.ToString("yyyyMMdd");
        var seq = 1;
        for (var i = 0; i < 200; i++)
        {
            var no = $"{prefix}{datePart}-{seq:D3}";
            if (!await existsAsync(no))
            {
                return no;
            }

            seq++;
        }

        throw new InvalidOperationException($"{prefix} 单号生成失败（号段冲突），请稍后重试");
    }
}
