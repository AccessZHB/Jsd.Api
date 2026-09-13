namespace Jsd.Api.Models.Common;

/// <summary>
/// 统一 API 返回格式（前端固定按 Code / Message / Data 解析）
/// </summary>
/// <typeparam name="T">业务数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>状态码：200 成功，401 未认证，403 无权限，400 业务错误</summary>
    public int Code { get; set; }

    /// <summary>提示信息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>业务数据</summary>
    public T? Data { get; set; }

    /// <summary>构造成功响应</summary>
    public static ApiResponse<T> Success(T data, string message = "操作成功")
        => new() { Code = 200, Message = message, Data = data };

    /// <summary>构造失败响应</summary>
    public static ApiResponse<T> Fail(string message, int code = 400)
        => new() { Code = code, Message = message, Data = default };
}
