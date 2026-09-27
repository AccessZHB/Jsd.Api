using Quartz;

namespace Jsd.Api.Jobs;

/// <summary>Quartz JobDataMap 取值辅助（自动任务模块）</summary>
public static class JobDataHelper
{
    /// <summary>本次执行的输出摘要键 —— 用 Quartz 的 FireInstanceId 保证同一次执行不串数据</summary>
    public static string ResultKey(IJobExecutionContext context)
        => context.FireInstanceId ?? context.JobDetail.Key.ToString();

    /// <summary>
    /// 读取字符串型参数（数字/布尔等原始类型也能读回）。
    /// ⚠️ 必须用 Try* 系列：JobDataMap.GetString(key) 在 key 不存在时会抛
    ///    KeyNotFoundException，而「任务参数没配」是完全正常的场景。
    /// </summary>
    public static string? GetString(JobDataMap map, string key)
    {
        if (map.TryGetString(key, out var s) && !string.IsNullOrWhiteSpace(s)) return s;

        // 参数以 JSON 数字/布尔落库时 GetString 取不到，退回原始值的字符串表示
        if (map.TryGetValue(key, out var raw) && raw is not null)
        {
            var text = raw.ToString();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }

    /// <summary>参数是否显式配置过（key 存在且非空）。用来在结果里提示「走了默认值」</summary>
    public static bool Has(JobDataMap map, string key)
        => map.TryGetValue(key, out var raw) && raw is not null
           && !string.IsNullOrWhiteSpace(raw.ToString());

    /// <summary>
    /// 读取整型参数（字符串数字、long、int 都能解析）。
    /// key 不存在或值非法时返回 defaultValue，绝不抛异常 ——
    /// 否则「建任务时没填参数」会直接把任务打成执行失败。
    /// </summary>
    public static int GetInt(JobDataMap map, string key, int defaultValue)
    {
        if (map.TryGetInt(key, out var i)) return i;

        if (map.TryGetValue(key, out var raw) && raw is not null
            && int.TryParse(raw.ToString(), out var fromObject)) return fromObject;

        if (map.TryGetString(key, out var s) && int.TryParse(s, out var fromString)) return fromString;

        return defaultValue;
    }

    /// <summary>读取布尔型参数（"true"/"false"/1/0 都认）；不存在或非法时返回 defaultValue</summary>
    public static bool GetBool(JobDataMap map, string key, bool defaultValue)
    {
        if (map.TryGetBoolean(key, out var b)) return b;

        if (map.TryGetValue(key, out var raw) && raw is not null)
        {
            var text = raw.ToString();
            if (bool.TryParse(text, out var fromString)) return fromString;
            if (int.TryParse(text, out var fromNumber)) return fromNumber != 0;
        }

        return defaultValue;
    }
}
