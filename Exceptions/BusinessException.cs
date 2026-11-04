namespace Jsd.Api.Exceptions;

/// <summary>
/// 业务异常：用于表达「可预期的业务规则拒绝」（如优惠金额超过订单总额、库存不足、状态不允许等），
/// 消息会原样返回给前端展示，**不应**被当成 500 系统错误。
///
/// 【为什么继承 InvalidOperationException】
/// 本项目所有 Controller 的既有写法是：
///     try { return await _service.XxxAsync(dto); }
///     catch (InvalidOperationException ex) { return ApiResponse&lt;T&gt;.Fail(ex.Message); }
/// 让 BusinessException 继承 InvalidOperationException，可以**零改动**复用这套捕获逻辑，
/// 既拿到了语义明确的异常类型，也不需要额外引入全局异常中间件（避免影响存量接口行为）。
/// </summary>
public class BusinessException : InvalidOperationException
{
    /// <summary>用业务提示消息构造异常</summary>
    /// <param name="message">面向用户的错误提示（会直接展示在小程序/后台）</param>
    public BusinessException(string message) : base(message)
    {
    }
}
