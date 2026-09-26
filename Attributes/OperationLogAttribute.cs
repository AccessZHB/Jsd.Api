namespace Jsd.Api.Attributes;

/// <summary>
/// 操作日志标记特性：标记在需要留痕的"写操作"接口上（POST/PUT/DELETE/PATCH）。
/// 由全局过滤器 OperationLogFilter 拦截采集，经 Channel 异步写入 sys_operation_log。
/// 【避坑】GET 查询接口不要标记，避免日志表膨胀过快（文档 6.1/九-1）。
/// 用法：[OperationLog("商品管理", "新增商品")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OperationLogAttribute : Attribute
{
    /// <summary>操作模块（如：商品管理）</summary>
    public string Module { get; }

    /// <summary>操作动作（如：新增商品）</summary>
    public string Action { get; }

    public OperationLogAttribute(string module, string action)
    {
        Module = module;
        Action = action;
    }
}
