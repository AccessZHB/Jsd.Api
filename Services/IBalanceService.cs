using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Services;

/// <summary>
/// 会员余额服务接口（B组：充值与余额）
///
/// 【资金安全铁律】
///   1) 所有余额变动（充值/冻结/解冻/扣减/调整）必须在数据库事务内完成；
///   2) 变动前后必须写 mkt_balance_log，且 before_balance / after_balance
///      必须与 mem_member 实时余额严格勾稽，作为对账真值；
///   3) 冻结/扣减使用 CAS 乐观锁（UPDATE ... WHERE balance = @current）防超扣；
///   4) 微信回调必须基于 transaction_id 做幂等，重复回调直接返回成功，严禁重复入账。
///
/// 【业务流程】先充值后下单：余额校验 → 冻结 → 创建订单 → 写价格快照 → 支付扣减；
///            取消/退款：解冻 → 退回余额 → 更新流水。
/// </summary>
public interface IBalanceService
{
    // ==================== 一、充值 ====================

    /// <summary>创建充值订单（生成 recharge_no，返回支付参数）</summary>
    Task<ApiResponse<RechargePayParamsDto>> CreateRechargeAsync(RechargeCreateDto dto);

    /// <summary>微信支付回调：幂等校验后事务内入账（balance + gift、total_recharge 累加、写流水）</summary>
    Task<WechatNotifyResultDto> HandleWechatNotifyAsync(WechatNotifyDto dto);

    /// <summary>分页查询充值记录（后台充值管理列表）</summary>
    Task<ApiResponse<PagedResult<RechargeDto>>> GetRechargesAsync(RechargeQueryDto q);

    /// <summary>充值单详情（含支付回调结果与会员当前余额）</summary>
    Task<ApiResponse<RechargeDto>> GetRechargeDetailAsync(long id);

    /// <summary>后台代客充值（线下收款，直接入账，无需回调）</summary>
    Task<ApiResponse<RechargeDto>> AdminRechargeAsync(AdminRechargeDto dto);

    /// <summary>充值退款（仅已支付可退；事务内扣回余额并写流水）</summary>
    Task<ApiResponse<bool>> RefundRechargeAsync(long id, RechargeRefundDto dto);

    // ==================== 二、余额查询 ====================

    /// <summary>查询会员余额概况（可用/冻结/累计充值/等级）</summary>
    Task<ApiResponse<MemberBalanceDto>> GetMemberBalanceAsync(long memMemberId);

    /// <summary>分页查询余额流水</summary>
    Task<ApiResponse<PagedResult<BalanceLogDto>>> GetLogsAsync(BalanceLogQueryDto q);

    // ==================== 三、余额操作 ====================

    /// <summary>后台手工调整（记录 operator_id 与原因，事务内完成）</summary>
    Task<ApiResponse<bool>> AdminAdjustAsync(AdminAdjustBalanceDto dto);

    /// <summary>下单冻结：balance -= 金额，frozen_balance += 金额</summary>
    Task<ApiResponse<bool>> FreezeAsync(FreezeBalanceDto dto);

    /// <summary>取消/退款解冻：frozen_balance -= 金额，balance += 金额</summary>
    Task<ApiResponse<bool>> UnfreezeAsync(UnfreezeBalanceDto dto);

    /// <summary>下单扣减：frozen_balance -= 金额（支付成功时把冻结额销账）</summary>
    Task<ApiResponse<bool>> DeductAsync(DeductBalanceDto dto);

    // ==================== 四、供订单/售后模块内部复用 ====================

    /// <summary>
    /// 校验下单余额是否充足（先充值后下单的唯一入口）。
    /// 返回失败消息时，订单服务必须拦截下单。
    /// </summary>
    Task<ApiResponse<bool>> CheckEnoughAsync(long memMemberId, decimal amount);

    /// <summary>
    /// 订单冻结（订单模块直接调用，免去 DTO 转换）。
    /// relatedType 为关联单据类型（BalanceRelatedType），不传默认记「订单」。
    /// </summary>
    Task FreezeForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null);

    /// <summary>订单支付成功后扣减冻结额</summary>
    Task DeductForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null);

    /// <summary>订单取消/关闭后解冻退回可用余额</summary>
    Task UnfreezeForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null);

    /// <summary>退款执行成功后把金额退回会员可用余额（change_type=3）</summary>
    Task RefundBackAsync(long memMemberId, decimal amount, long refundId, string? remark = null);
}
