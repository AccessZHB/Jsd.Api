using System.Collections.Concurrent;

namespace Jsd.Api.Jobs;

/// <summary>
/// 任务执行结果摘要的线程安全暂存区（自动任务模块）
///
/// 背景：Quartz 的 IJobListener 与业务 Job 处于不同的执行上下文，业务里
/// `JobDataHelper.Context.Add(...)` 这种「借静态变量传话」的写法在并发执行同一任务时会互相串数据。
/// 这里改按 Quartz 的 FireInstanceId 分桶：同一次执行只读写自己那一格，listener 取完即删。
/// </summary>
public static class JobResultBuffer
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> Buckets = new();

    /// <summary>写入（同一 key 多次写入按先后顺序拼接）</summary>
    public static void Push(string key, string message)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message)) return;

        var q = Buckets.GetOrAdd(key, _ => new ConcurrentQueue<string>());
        q.Enqueue(message);
    }

    /// <summary>取出并清空（取不到返回 null）</summary>
    public static string? Take(string key)
    {
        if (string.IsNullOrEmpty(key) || !Buckets.TryRemove(key, out var q) || q.IsEmpty) return null;

        var parts = q.ToArray();
        var text = string.Join("；", parts);

        // TEXT 列有长度上限，超长截断并保留尾部最新内容
        return text.Length <= 2000 ? text : "…" + text[^2000..];
    }

    /// <summary>丢弃一次执行的暂存（如任务被否决时清理脏数据）</summary>
    public static void Drop(string key)
    {
        if (!string.IsNullOrEmpty(key)) Buckets.TryRemove(key, out _);
    }
}
