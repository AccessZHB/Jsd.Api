using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Models.Order;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 收款与核销 服务实现（菜单 322）
///
/// 【职责】
///   1) 录入线下收款单（mkt_payment）
///   2) 核销：把一笔收款拆分挂到多张应收账款（mkt_payment_item + 回写 trx_receivable）
///   3) 微信支付回调：写 trx_payment_log + 更新 trx_order 状态
///
/// 【事务约定】
///   核销涉及 3 张表的写入（mkt_payment_item / trx_receivable / mkt_payment），
///   必须整体成功或整体回滚，因此统一用 DbContext.Database 事务包裹。
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReceivableRepository _receivableRepository;
    private readonly IRepository<TrxOrder> _orderRepository;
    private readonly IRepository<MemMember> _memberRepository;
    private readonly IMapper _mapper;
    private readonly PaymentCreateValidator _createValidator;
    private readonly WriteOffInputValidator _writeOffValidator;

    public PaymentService(
        AppDbContext db,
        IPaymentRepository paymentRepository,
        IReceivableRepository receivableRepository,
        IRepository<TrxOrder> orderRepository,
        IRepository<MemMember> memberRepository,
        IMapper mapper,
        PaymentCreateValidator createValidator,
        WriteOffInputValidator writeOffValidator)
    {
        _db = db;
        _paymentRepository = paymentRepository;
        _receivableRepository = receivableRepository;
        _orderRepository = orderRepository;
        _memberRepository = memberRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _writeOffValidator = writeOffValidator;
    }

    // ==================== 1. 列表 / 详情 ====================

    public async Task<PagedResult<PaymentListDto>> GetListAsync(PaymentPageQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

        var (rows, total) = await _paymentRepository.GetPagedListAsync(
            query.MemberId, query.MemberName, query.PaymentNo, query.Status,
            query.StartTime, query.EndTime, page, pageSize);

        var list = rows.Select(row =>
        {
            var dto = _mapper.Map<PaymentListDto>(row.Payment);
            dto.MemberName = row.MemberName;
            return dto;
        }).ToList();

        return PagedResult<PaymentListDto>.Create(list, total, page, pageSize);
    }

    public async Task<PaymentDetailDto?> GetDetailAsync(long id)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);
        if (payment == null) return null;

        var dto = _mapper.Map<PaymentDetailDto>(payment);

        var member = await _memberRepository.GetByIdAsync(payment.MemberId);
        if (member != null) dto.MemberName = member.Name;

        // 核销明细（含被核销订单号）
        var items = await _paymentRepository.GetWriteOffItemsAsync(id);
        dto.Items = items.Select(i => new PaymentItemDto
        {
            Id = i.Item.Id,
            ReceivableId = i.Item.ReceivableId,
            OrderNo = i.OrderNo,
            OffsetAmount = i.Item.OffsetAmount,
            CreateTime = i.Item.CreateTime
        }).ToList();

        return dto;
    }

    // ==================== 2. 录入收款单 ====================

    /// <summary>
    /// 录入线下收款单。
    /// 收款单号后端自动生成（PAY + 时间戳 + 随机数），初始状态 = 待核销(0)。
    /// </summary>
    public async Task<PaymentCreateResultDto> CreatePaymentAsync(PaymentCreateDto dto, int operatorId)
    {
        // FluentValidation 参数校验（失败抛 ValidationException）
        _createValidator.ValidateAndThrow(dto);

        // 客户必须存在
        var member = await _memberRepository.GetByIdAsync(dto.MemberId);
        if (member == null)
        {
            throw new InvalidOperationException("客户不存在");
        }

        var payment = new MktPayment
        {
            PaymentNo = await _paymentRepository.GeneratePaymentNoAsync(),
            MemberId = dto.MemberId,
            Amount = decimal.Round(dto.Amount, 2),
            PaymentMethod = dto.PaymentMethod,
            PaymentDate = dto.PaymentDate.Date,
            Voucher = dto.Voucher,
            Remark = dto.Remark,
            OperatorId = operatorId,
            Status = PaymentStatus.Pending   // 待核销
        };

        await _paymentRepository.AddAsync(payment);
        await _paymentRepository.SaveChangesAsync();

        return new PaymentCreateResultDto { Id = payment.Id, PaymentNo = payment.PaymentNo };
    }

    // ==================== 3. 核销（核心） ====================

    /// <summary>
    /// 【核心】核销：把一笔收款拆分挂到多张应收账款上。
    ///
    /// 完整流程（全部在一个数据库事务内）：
    ///   1) FluentValidation 入参校验
    ///   2) 校验收款单存在，且状态为"待核销(0)"（已核销不可重复核销）
    ///   3) 校验【核销明细总金额 == 收款单金额】（必须刚好核销完，不允许留有余额）
    ///   4) 逐条校验应收单：存在 / 同客户 / 未结清 / 核销额不超过未收额
    ///   5) 插入 mkt_payment_item 核销明细
    ///   6) 回写 trx_receivable：paid_amount 累加、unpaid_amount 扣减
    ///   7) 自动更新应收单状态：unpaid == 0 → 已结清(1)；否则保持未结清(0)
    ///      （"部分结清"无独立状态码，由 paid&gt;0 且 unpaid&gt;0 推导，见 ReceivableStatus.GetSettleName）
    ///   8) 更新收款单状态 → 已核销(1)
    ///   9) 提交事务；任何异常整体回滚
    /// </summary>
    public async Task WriteOffAsync(WriteOffInputDto dto)
    {
        // 1) 入参校验
        _writeOffValidator.ValidateAndThrow(dto);

        // 开启数据库事务：核销涉及多表写入，必须原子性
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 2) 收款单校验（带跟踪，后续要改状态）
            var payment = await _paymentRepository.GetByIdTrackedAsync(dto.PaymentId);
            if (payment == null)
            {
                throw new InvalidOperationException("收款单不存在");
            }
            if (payment.Status != PaymentStatus.Pending)
            {
                throw new InvalidOperationException("该收款单已核销，不可重复核销");
            }

            // 3) 核销总金额必须恰好等于收款金额（不允许多销或少销）
            var totalOffset = decimal.Round(dto.Items.Sum(i => i.Amount), 2);
            if (totalOffset != payment.Amount)
            {
                throw new InvalidOperationException(
                    $"核销总金额({totalOffset})必须等于收款金额({payment.Amount})");
            }

            // 4)~7) 逐条处理应收单
            var paymentItems = new List<MktPaymentItem>();

            foreach (var item in dto.Items)
            {
                var receivable = await _receivableRepository.GetByIdTrackedAsync(item.ReceivableId);
                if (receivable == null)
                {
                    throw new InvalidOperationException($"应收账款 {item.ReceivableId} 不存在");
                }

                // 不允许跨客户核销（A 客户的钱不能还 B 客户的账）
                if (receivable.MemberId != payment.MemberId)
                {
                    throw new InvalidOperationException(
                        $"应收账款 {receivable.Id} 不属于该客户，不可跨客户核销");
                }

                // 已结清的应收单不可再次核销
                if (receivable.Status == ReceivableStatus.Settled || receivable.UnpaidAmount <= 0)
                {
                    throw new InvalidOperationException(
                        $"应收账款 {receivable.Id} 已结清，不可再次核销");
                }

                // 核销金额不能超过未收金额（防止超额核销导致 unpaid 变负数）
                var offset = decimal.Round(item.Amount, 2);
                if (offset > receivable.UnpaidAmount)
                {
                    throw new InvalidOperationException(
                        $"应收账款 {receivable.Id} 的核销金额({offset})超过未收金额({receivable.UnpaidAmount})");
                }

                // 5) 构造核销明细
                paymentItems.Add(new MktPaymentItem
                {
                    PaymentId = payment.Id,
                    ReceivableId = receivable.Id,
                    OffsetAmount = offset
                });

                // 6) 回写应收：累加已收、扣减未收
                receivable.PaidAmount = decimal.Round(receivable.PaidAmount + offset, 2);
                receivable.UnpaidAmount = decimal.Round(receivable.UnpaidAmount - offset, 2);

                // 7) 自动更新状态
                if (receivable.UnpaidAmount <= 0)
                {
                    receivable.UnpaidAmount = 0;
                    receivable.Status = ReceivableStatus.Settled;   // 已结清
                }
                // 否则保持未结清(0)，前端按 paid>0 && unpaid>0 展示为"部分结清"

                _receivableRepository.Update(receivable);
            }

            // 5) 批量插入核销明细
            await _paymentRepository.AddItemsAsync(paymentItems);

            // 8) 更新收款单状态 → 已核销
            payment.Status = PaymentStatus.WrittenOff;
            _paymentRepository.Update(payment);

            // 一次性提交：mkt_payment_item + trx_receivable + mkt_payment
            await _paymentRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            // 任何异常：整体回滚，保证不会出现"扣了应收但没记明细"的脏数据
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ==================== 4. 支付日志 / 微信回调 ====================

    public async Task<List<PaymentLogDto>> GetLogsByOrderIdAsync(long orderId)
    {
        var logs = await _paymentRepository.GetLogsByOrderIdAsync(orderId);
        return _mapper.Map<List<PaymentLogDto>>(logs);
    }

    /// <summary>
    /// 查询某订单的支付回调日志（订单详情"支付记录"时间轴专用）。
    /// 按回调时间倒序，最新一次回调排在最上面，便于排查掉单。
    /// </summary>
    public async Task<List<OrderPaymentLogDto>> GetOrderPaymentLogsAsync(long orderId)
    {
        var logs = await _paymentRepository.GetLogsByOrderIdAsync(orderId);

        // 同一微信交易号出现多次 = 微信重复推送过回调（正常应被幂等拦截，不会真的重复写库）
        var duplicateIds = logs
            .Where(l => !string.IsNullOrWhiteSpace(l.TransactionId))
            .GroupBy(l => l.TransactionId)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(l => l.Id))
            .ToHashSet();

        return logs.Select(l => new OrderPaymentLogDto
        {
            Id = l.Id,
            OrderId = l.OrderId,
            OrderNo = l.OrderNo,
            WechatTransactionId = l.TransactionId,
            OutTradeNo = l.OutTradeNo,
            Amount = l.Amount,
            NotifyTime = l.CallbackTime,
            Status = l.Status,
            Success = l.Status == PayLogStatus.Paid,
            StatusText = ToNotifyStatusText(l.Status),
            RawXml = l.CallbackData,
            ErrorCode = l.ErrorCode,
            ErrorMsg = l.ErrorMsg,
            CreateTime = l.CreateTime,
            IsDuplicate = duplicateIds.Contains(l.Id)
        }).ToList();
    }

    /// <summary>支付状态 → 时间轴标题用文本（成功 / 失败 / …）</summary>
    private static string ToNotifyStatusText(int status) => status switch
    {
        PayLogStatus.Paid => "成功",
        PayLogStatus.Pending => "待支付",
        PayLogStatus.Cancelled => "已取消",
        PayLogStatus.Refunded => "已退款",
        PayLogStatus.Failed => "失败",
        _ => "未知"
    };

    /// <summary>
    /// 处理微信支付回调：解析 → 验签 → 【幂等判重】→ 写 trx_payment_log → 更新 trx_order 状态。
    ///
    /// ╔══════════════════════════════════════════════════════════════════════╗
    /// ║ 【幂等性设计 · 重点】                                                 ║
    /// ║ 微信在网络抖动时会【重复推送同一笔回调】，如果每次都处理一遍，         ║
    /// ║ 会导致订单金额/状态被重复更新、账目翻倍。因此必须做两层防护：          ║
    /// ║                                                                        ║
    /// ║ 第一道：按 wechat_transaction_id（微信交易号）查 trx_payment_log       ║
    /// ║   - 已存在 且 status = 已支付(1) → 直接返回已有日志ID，                ║
    /// ║     【不写新日志、不推进订单、不累加任何金额】；                       ║
    /// ║   - 已存在 但此前是失败/待支付 → 更新【同一条】记录为本次结果，        ║
    /// ║     不新增行（避免同一交易号产生多条日志，干扰对账）。                 ║
    /// ║                                                                        ║
    /// ║ 第二道：transaction_id 为空时，退化为按 out_trade_no（商户订单号）判重 ║
    /// ║                                                                        ║
    /// ║ 并发防护：判重 + 写日志 + 推进订单 全部包在同一个数据库事务里，        ║
    /// ║   避免"两个回调同时进来，都查到不存在，然后都插入"的竞态。             ║
    /// ║   （最稳妥的做法是再给 transaction_id 加唯一索引，见文末备注。）       ║
    /// ╚══════════════════════════════════════════════════════════════════════╝
    ///
    /// 其他说明：
    ///   - 验签（sign 校验）需要商户 API 密钥，本项目暂未配置，
    ///     这里保留验签入口与 TODO，生产环境接入后替换 VerifySign 实现即可。
    ///   - 支付成功时把订单置为"已支付(1)"并写入支付时间/支付方式；
    ///     订单金额（pay_amount）在下单时已确定，回调【不修改任何金额字段】。
    ///   - 库存扣减由 OrderService.PayAsync 统一负责，回调里不重复扣减。
    /// </summary>
    public async Task<WechatCallbackResultDto> HandleWechatCallbackAsync(WechatCallbackDto dto)
    {
        var now = DateTime.Now;
        var isSuccess = string.Equals(dto.ResultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase);

        // 1) 解析：按商户订单号定位订单（out_trade_no 即 trx_order.order_no）
        var order = await _orderRepository.FirstOrDefaultAsync(o => o.OrderNo == dto.OutTradeNo);

        // 2) 验签（TODO：接入商户密钥后启用真实验签）
        // if (!VerifySign(dto)) { throw new InvalidOperationException("微信回调验签失败"); }

        // 判重 + 落库 + 推进订单状态，整体放在一个事务里，杜绝并发重复处理
        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // ---- 幂等第一道防线：微信交易号 transaction_id ----
            TrxPaymentLog? log = null;
            if (!string.IsNullOrWhiteSpace(dto.TransactionId))
            {
                log = await _paymentRepository.GetLogByTransactionIdTrackedAsync(dto.TransactionId);
            }

            // ---- 幂等第二道防线：商户订单号 out_trade_no（交易号为空时兜底）----
            log ??= await _paymentRepository.GetLogByOutTradeNoTrackedAsync(dto.OutTradeNo);

            if (log != null && log.Status == PayLogStatus.Paid)
            {
                // ⚠️ 同一笔交易已成功处理过：直接返回"已处理"，
                //    绝不重复写日志 / 不推进订单 / 不累加任何金额
                await tx.RollbackAsync();
                return new WechatCallbackResultDto
                {
                    LogId = log.Id,
                    Duplicated = true,
                    Message = "该回调已处理过，已忽略（幂等）"
                };
            }

            if (log == null)
            {
                // 3) 首次回调：写入支付日志
                log = new TrxPaymentLog
                {
                    OrderId = order?.Id ?? 0,
                    OrderNo = dto.OutTradeNo,
                    TransactionId = dto.TransactionId,
                    OutTradeNo = dto.OutTradeNo,
                    PaymentType = PayLogTypes.Wechat,
                    Amount = decimal.Round(dto.Amount, 2),
                    Status = isSuccess ? PayLogStatus.Paid : PayLogStatus.Failed,
                    CallbackData = dto.RawData,
                    CallbackTime = now,
                    ErrorCode = isSuccess ? null : dto.ErrCode,
                    ErrorMsg = isSuccess ? null : dto.ErrMsg
                };
                await _paymentRepository.AddLogAsync(log);
            }
            else
            {
                // 3') 同一笔交易此前是失败/待支付，本次是最终结果：
                //     更新【原来的那一条】，不新增行（保持一交易号一日志，便于对账）
                //     实体是从仓储带跟踪查出的，直接改属性即可，SaveChanges 会自动更新
                log.Status = isSuccess ? PayLogStatus.Paid : PayLogStatus.Failed;
                log.Amount = decimal.Round(dto.Amount, 2);
                log.CallbackData = dto.RawData ?? log.CallbackData;
                log.CallbackTime = now;
                log.ErrorCode = isSuccess ? null : dto.ErrCode;
                log.ErrorMsg = isSuccess ? null : dto.ErrMsg;
                log.OrderId = log.OrderId > 0 ? log.OrderId : (order?.Id ?? 0);
            }

            // 4) 支付成功 → 更新订单状态为"已支付(待发货)"
            if (isSuccess)
            {
                if (order == null)
                {
                    throw new InvalidOperationException($"订单 {dto.OutTradeNo} 不存在，支付回调无法处理");
                }

                // 仅"待付款"订单才推进：已支付/已发货/已完成/已关闭的一律不动，保证幂等。
                // 注意：只改状态与时间，【不动任何金额字段】（pay_amount 下单时已确定）。
                if (order.OrderStatus == 0)
                {
                    order.OrderStatus = OrderStatuses.Paid;   // 0 待付款 → 1 已支付(待发货)
                    order.PayMethod = 1;                      // 1-在线支付
                    order.PayTime = now;
                    _orderRepository.Update(order);
                }
            }

            await _paymentRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return new WechatCallbackResultDto
            {
                LogId = log.Id,
                Duplicated = false,
                Message = isSuccess ? "支付回调处理成功" : "支付失败回调已记录"
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 微信回调验签占位实现。
    /// 生产环境需使用商户 API v3 密钥对 RawData 做签名比对，比对通过才返回 true。
    /// </summary>
    private static bool VerifySign(WechatCallbackDto dto)
    {
        // TODO: 接入微信支付商户密钥后实现真实验签
        return !string.IsNullOrEmpty(dto.Sign);
    }
}
