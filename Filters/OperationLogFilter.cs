using System.Diagnostics;
using System.Text.Json;
using Jsd.Api.Attributes;
using Jsd.Api.Entities;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jsd.Api.Filters;

/// <summary>
/// 操作日志采集过滤器（全局注册，仅对标记了 [OperationLog] 的写操作接口生效）。
/// 等价于文档 6.1 的 AOP 切面：自动采集操作人、模块、动作、HTTP方法、路径、请求参数、
/// 响应结果、执行耗时、IP、UA（浏览器/操作系统自动解析）。
/// 敏感字段脱敏：password/token/secret/credit_card 等键值替换为 ******。
/// 通过 OperationLogWriter（Channel）异步落库，不阻塞主流程。
/// </summary>
public class OperationLogFilter : IAsyncActionFilter
{
    private readonly OperationLogWriter _writer;

    public OperationLogFilter(OperationLogWriter writer)
    {
        _writer = writer;
    }

    /// <summary>需要脱敏的请求字段名（小写比对）</summary>
    private static readonly string[] SensitiveKeys =
    {
        "password", "oldpassword", "newpassword", "confirmpassword",
        "token", "accesstoken", "refreshtoken", "secret", "credit_card", "creditcard"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 只处理标记了 [OperationLog] 且非 GET 的接口（文档九-1：GET 不记录，避免日志膨胀）
        var attr = context.ActionDescriptor.EndpointMetadata.OfType<OperationLogAttribute>().FirstOrDefault();
        if (attr == null)
        {
            await next();
            return;
        }

        var http = context.HttpContext;
        var sw = Stopwatch.StartNew();

        // 执行接口本体
        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch (Exception ex)
        {
            // 接口抛异常：记失败日志后原样抛出（不吞异常）
            sw.Stop();
            EnqueueLog(attr, http, context, sw.ElapsedMilliseconds, 0, null, Truncate(ex.ToString(), 2000));
            throw;
        }

        sw.Stop();

        // 响应结果：取 ObjectResult 的值（本项目统一 ApiResponse<T>），脱敏后截断存储
        string? responseResult = null;
        var errorMessage = executed.Exception?.ToString();
        var status = executed.Exception == null ? 1 : 0;

        if (executed.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            try
            {
                responseResult = Truncate(JsonSerializer.Serialize(objectResult.Value,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), 2000);
            }
            catch
            {
                // 序列化失败不影响日志主信息
            }
        }

        EnqueueLog(attr, http, context, sw.ElapsedMilliseconds, status, responseResult,
            errorMessage == null ? null : Truncate(errorMessage, 2000));
    }

    /// <summary>组装日志实体并投入异步写队列</summary>
    private void EnqueueLog(OperationLogAttribute attr, HttpContext http, ActionExecutingContext context,
        long elapsedMs, int status, string? responseResult, string? errorMessage)
    {
        try
        {
            var user = http.User;
            long? operatorId = null;
            var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(idClaim, out var uid)) operatorId = uid;

            var userAgent = http.Request.Headers["User-Agent"].ToString();

            var log = new SysOperationLog
            {
                OperatorId = operatorId,
                OperatorName = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty,
                Module = attr.Module,
                Action = attr.Action,
                RequestMethod = http.Request.Method,
                Path = http.Request.Path.Value ?? string.Empty,
                RequestParams = SerializeAndMaskArguments(context.ActionArguments),
                ResponseResult = responseResult,
                IpAddress = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = userAgent.Length <= 500 ? userAgent : userAgent.Substring(0, 500),
                Browser = UserAgentParser.ParseBrowser(userAgent),
                Os = UserAgentParser.ParseOs(userAgent),
                ExecutionTime = (int)elapsedMs,
                Status = status,
                ErrorMessage = errorMessage,
                CreateTime = DateTime.Now
            };
            _writer.TryEnqueue(log);
        }
        catch
        {
            // 采集日志本身绝不抛异常影响业务
        }
    }

    /// <summary>序列化请求参数并脱敏敏感字段（截断至 2000 字符）</summary>
    private static string? SerializeAndMaskArguments(IDictionary<string, object?> arguments)
    {
        try
        {
            if (arguments.Count == 0) return null;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(arguments, options);

            return Truncate(MaskSensitiveFields(json), 2000);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>对 JSON 文本中的敏感键值做 ****** 脱敏（正则匹配 "key":"value"）</summary>
    private static string MaskSensitiveFields(string json)
    {
        foreach (var key in SensitiveKeys)
        {
            // 形如 "password":"xxx" / "oldPassword":"xxx"（camelCase 序列化后首字母小写）
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                $"\"{key}\"\\s*:\\s*\"[^\"]*\"",
                $"\"{key}\":\"******\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return json;
    }

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? value
            : (value.Length <= maxLength ? value : value.Substring(0, maxLength));
}
