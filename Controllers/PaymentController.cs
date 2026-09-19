using FluentValidation;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 收款与核销接口（对应菜单 322：/finance/payment）
///
/// 权限：
///   finance:payment:list    查看收款记录
///   finance:payment:add     录入收款
///   finance:payment:verify  核销账单
/// </summary>
[ApiController]
[Route("api/payment")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly CurrentUserService _currentUser;

    public PaymentController(IPaymentService paymentService, CurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 分页查询收款记录
    /// 示例：GET /api/payment/page?page=1&amp;pageSize=10&amp;memberName=张&amp;status=0
    /// </summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<PaymentListDto>>> GetPage([FromQuery] PaymentPageQuery query)
    {
        var result = await _paymentService.GetListAsync(query);
        return ApiResponse<PagedResult<PaymentListDto>>.Success(result);
    }

    /// <summary>
    /// 收款单详情（含核销明细）
    /// 示例：GET /api/payment/1/detail
    /// </summary>
    [HttpGet("{id}/detail")]
    public async Task<ApiResponse<PaymentDetailDto>> GetDetail(long id)
    {
        var dto = await _paymentService.GetDetailAsync(id);
        if (dto == null)
        {
            return ApiResponse<PaymentDetailDto>.Fail("收款单不存在");
        }
        return ApiResponse<PaymentDetailDto>.Success(dto);
    }

    /// <summary>
    /// 录入线下收款单（自动生成收款单号，状态=待核销）
    /// 请求体：{ "memberId":1,"amount":5000.00,"paymentMethod":1,"paymentDate":"2026-09-15","voucher":"","remark":"" }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<PaymentCreateResultDto>> Create([FromBody] PaymentCreateDto dto)
    {
        try
        {
            var operatorId = (int)_currentUser.UserId;
            var result = await _paymentService.CreatePaymentAsync(dto, operatorId);
            return ApiResponse<PaymentCreateResultDto>.Success(result, $"收款单 {result.PaymentNo} 已录入，待核销");
        }
        catch (ValidationException ex)
        {
            // FluentValidation 校验失败：把第一个错误提示返回给前端
            return ApiResponse<PaymentCreateResultDto>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<PaymentCreateResultDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 【核心】核销：把一笔收款拆分挂到多张应收账款上（事务操作）
    ///
    /// 请求体：
    /// {
    ///   "paymentId": 1,
    ///   "items": [ { "receivableId": 10, "amount": 3000.00 }, { "receivableId": 11, "amount": 2000.00 } ]
    /// }
    /// 约束：items 各 amount 之和必须恰好等于收款单金额。
    /// </summary>
    [HttpPost("{id}/write-off")]
    public async Task<ApiResponse<object>> WriteOff(long id, [FromBody] List<WriteOffDto> items)
    {
        var input = new WriteOffInputDto { PaymentId = id, Items = items ?? new List<WriteOffDto>() };

        try
        {
            await _paymentService.WriteOffAsync(input);
            return ApiResponse<object>.Success(new { paymentId = id }, "核销成功");
        }
        catch (ValidationException ex)
        {
            return ApiResponse<object>.Fail(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "参数校验失败");
        }
        catch (InvalidOperationException ex)
        {
            // 已核销 / 金额不符 / 超额核销 / 跨客户核销 等业务异常统一转 400
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 查询某订单的支付日志（内部对账用，trx_payment_log）
    /// 示例：GET /api/payment/logs/1001
    /// </summary>
    [HttpGet("logs/{orderId}")]
    public async Task<ApiResponse<List<PaymentLogDto>>> GetLogs(long orderId)
    {
        var logs = await _paymentService.GetLogsByOrderIdAsync(orderId);
        return ApiResponse<List<PaymentLogDto>>.Success(logs);
    }
}
