using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jsd.Api.Converters;

/// <summary>
/// 宽松版 DateTime JSON 转换器（全局注册于 Program.AddJsonOptions）。
///
/// 背景：System.Text.Json 默认只接受 ISO 8601（"2026-10-01T00:00:00"，T 分隔），
/// 而 Element Plus 的 el-date-picker 常用 value-format="YYYY-MM-DD HH:mm:ss"（空格分隔），
/// 导致前端提交含日期的表单（如价格策略有效期）时反序列化失败 → [ApiController] 自动返回 400。
///
/// 本转换器兼容以下输入（写入时统一输出 ISO 8601）：
///   "2026-10-01T00:00:00"  （ISO 8601，含带时区）
///   "2026-10-01 00:00:00"  （空格分隔，DateTime.Parse 可解析的其他常见格式）
/// 解析仍失败时抛 JsonException，由框架按原逻辑返回 400。
/// </summary>
public class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("日期时间不能为空");
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new JsonException($"无法解析的日期时间格式：{value}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
