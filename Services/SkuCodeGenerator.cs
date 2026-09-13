namespace Jsd.Api.Services;

/// <summary>
/// SKU 编码生成器（全局唯一入口，避免多处各写一套规则）。
/// 规则：固定前缀 + 时间戳 + 随机数
///      SKU + yyyyMMddHHmmss + 4位随机数   → 例：SKU202609082310451234
/// 长度：3 + 14 + 4 = 21 字符，远小于 prod_sku.sku_code 的 VARCHAR(50)。
/// </summary>
public static class SkuCodeGenerator
{
    /// <summary>编码固定前缀</summary>
    public const string Prefix = "SKU";

    /// <summary>时间戳格式（年4 + 月2 + 日2 + 时2 + 分2 + 秒2 = 14位）</summary>
    private const string TimestampFormat = "yyyyMMddHHmmss";

    /// <summary>随机数上限（4位：0000 ~ 9999）</summary>
    private const int RandomMaxExclusive = 10000;

    /// <summary>冲突后的最大重试次数</summary>
    private const int MaxRetry = 20;

    /// <summary>
    /// 生成一个 SKU 编码（不保证唯一，唯一性由 GenerateUniqueAsync 负责）。
    /// </summary>
    public static string Generate()
    {
        // 时间戳取当前本地时间；随机数用 Random.Shared（.NET 6+ 线程安全的共享实例）
        var timestamp = DateTime.Now.ToString(TimestampFormat);
        var randomPart = Random.Shared.Next(0, RandomMaxExclusive).ToString("D4");
        return string.Concat(Prefix, timestamp, randomPart);
    }

    /// <summary>
    /// 生成【批次内 + 数据库】都不重复的 SKU 编码。
    /// 同一批新增多个 SKU 时（时间戳同一秒），靠 4 位随机数 + 重试来区分。
    /// </summary>
    /// <param name="existsAsync">判断编码是否已在数据库中存在的委托</param>
    /// <param name="batchCodes">
    /// 本批次已用编码集合（可空）。用于避免一次保存多个 SKU 时批次内撞号；
    /// 传入后本方法会把新生成的编码写入其中。
    /// </param>
    public static async Task<string> GenerateUniqueAsync(
        Func<string, Task<bool>> existsAsync,
        HashSet<string>? batchCodes = null)
    {
        batchCodes ??= new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < MaxRetry; i++)
        {
            var code = Generate();

            // 1) 批次内去重
            if (!batchCodes.Add(code)) continue;

            // 2) 数据库去重（uk_sku_code 唯一索引生效前的业务层预校验）
            if (await existsAsync(code)) continue;

            return code;
        }

        // 极端兜底：20 次都撞号（概率极低），追加 8 位 GUID 片段，长度仍 < 50
        var fallback = string.Concat(Prefix, DateTime.Now.ToString(TimestampFormat),
                                     Guid.NewGuid().ToString("N")[..8]);
        batchCodes.Add(fallback);
        return fallback;
    }
}
