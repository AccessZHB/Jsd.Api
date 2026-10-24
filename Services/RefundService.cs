using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 退款申请服务（售后模块）
///
/// 【状态机 trx_refund.status】
///   0-待审核 Pending ──审核通过──&gt; 1-待退款 Approved
///   0 ──驳回──&gt; 3-已驳回 Rejected
///   1 ──执行退款──&gt; 2-已退款 Refunded（终态，幂等保护）
///
/// 【退款类型 refund_type】1-仅退款（不退实物） 2-退货退款（需仓库收货后退款）
///
/// 【事务】所有写操作在单个 SaveChanges 内原子提交（EF 默认事务包裹），
///        审核通过自动生成退货单、执行退款与退货收货联动均在同一事务内完成。
///
/// 【幂等】执行退款按 status 判重：已退款(2)直接返回成功，避免财务重复打款。
///
/// 【留痕】每次状态流转都写 trx_refund_log，与业务数据同事务提交。
/// </summary>
public class RefundService : IRefundService
{
    private const int MaxNoRetry = 5;

    private readonly AppDbContext _db;
    private readonly IRefundRepository _refundRepo;
    private readonly IRepository<TrxRefundLog> _logRepo;
    private readonly IRepository<TrxOrder> _orderRepo;
    private readonly IRepository<TrxOrderItem> _orderItemRepo;
    private readonly IReturnRepository _returnRepo;
    private readonly IRepository<TrxReturnItem> _returnItemRepo;
    private readonly IRepository<MemMember> _memberRepo;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly IBalanceService _balanceService;
    private readonly CreateRefundValidator _createValidator;
    private readonly ApproveRefundValidator _approveValidator;

    public RefundService(
        AppDbContext db,
        IRefundRepository refundRepo,
        IRepository<TrxRefundLog> logRepo,
        IRepository<TrxOrder> orderRepo,
        IRepository<TrxOrderItem> orderItemRepo,
        IReturnRepository returnRepo,
        IRepository<TrxReturnItem> returnItemRepo,
        IRepository<MemMember> memberRepo,
        IMapper mapper,
        CurrentUserService currentUser,
        IBalanceService balanceService,
        CreateRefundValidator createValidator,
        ApproveRefundValidator approveValidator)
    {
        _db = db;
        _refundRepo = refundRepo;
        _logRepo = logRepo;
        _orderRepo = orderRepo;
        _orderItemRepo = orderItemRepo;
        _returnRepo = returnRepo;
        _returnItemRepo = returnItemRepo;
        _memberRepo = memberRepo;
        _mapper = mapper;
        _currentUser = currentUser;
        _balanceService = balanceService;
        _createValidator = createValidator;
        _approveValidator = approveValidator;
    }

    // ============================================================
    // 1. 提交退款申请
    // ============================================================
    public async Task<ApiResponse<RefundCreateResult>> CreateAsync(CreateRefundDto dto)
    {
        _createValidator.ValidateAndThrow(dto);

        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.IsDeleted == 0);
        if (order == null)
        {
            return ApiResponse<RefundCreateResult>.Fail("订单不存在");
        }

        // 订单必须已完成（order_status=3），未完成的订单不允许退款
        if (order.OrderStatus != 3)
        {
            return ApiResponse<RefundCreateResult>.Fail("仅已完成的订单可申请退款");
        }

        // 未全额退款：已占用额度（待退款+已退款）不可超过订单实付
        var refundedSum = await _refundRepo.GetRefundedSumByOrderAsync(order.Id);
        var available = order.PayAmount - refundedSum;
        if (dto.RefundAmount > available)
        {
            return ApiResponse<RefundCreateResult>.Fail(
                $"退款金额超出可退额度（订单实付 {order.PayAmount:0.00}，已申请 {refundedSum:0.00}，可退 {available:0.00}）");
        }

        // 单号生成 + 唯一索引兜底重试
        TrxRefund? refund = null;
        for (var i = 0; i < MaxNoRetry; i++)
        {
            var candidate = _refundRepo.GenerateRefundNo();
            if (await _refundRepo.ExistsRefundNoAsync(candidate))
            {
                continue;
            }

            refund = new TrxRefund
            {
                RefundNo = candidate,
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                MemberId = order.BuyerId,
                RefundAmount = dto.RefundAmount,
                RefundType = dto.RefundType,
                RefundReason = dto.RefundReason,
                VoucherImages = dto.VoucherImages,
                Status = (int)RefundStatus.Pending
            };

            await _refundRepo.AddAsync(refund);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                // 极小概率撞号（并发），清空重试
                refund = null;
            }
        }

        if (refund == null)
        {
            return ApiResponse<RefundCreateResult>.Fail("生成退款单号失败，请稍后重试");
        }

        // 提交申请留痕
        await AppendLogAsync(refund.Id, RefundLogActions.Created, _currentUser.UserId,
            _currentUser.UserName, $"提交退款申请（{RefundTypeHelper.GetName(refund.RefundType)}）");
        await _db.SaveChangesAsync();

        return ApiResponse<RefundCreateResult>.Success(
            new RefundCreateResult { Id = refund.Id, RefundNo = refund.RefundNo },
            "退款申请已提交，等待审核");
    }

    // ============================================================
    // 2. 退款列表分页
    // ============================================================
    public async Task<ApiResponse<PagedResult<RefundListDto>>> GetPagedListAsync(RefundQueryDto q)
    {
        var (items, total) = await _refundRepo.GetPagedListAsync(q);

        // 批量补全 客户名称 / 订单实付金额，避免 N+1
        var memberIds = items.Select(r => r.MemberId).Distinct().ToList();
        var orderIds = items.Select(r => r.OrderId).Distinct().ToList();
        var memberNameMap = await _memberRepo.GetListAsync(m => memberIds.Contains(m.Id))
            .ContinueWith(t => t.Result.ToDictionary(m => m.Id, m => m.Name));
        var orderMap = await _orderRepo.GetListAsync(o => orderIds.Contains(o.Id))
            .ContinueWith(t => t.Result.ToDictionary(o => o.Id, o => o.PayAmount));

        var dtos = items.Select(r =>
        {
            var d = _mapper.Map<RefundListDto>(r);
            d.MemberName = memberNameMap.TryGetValue(r.MemberId, out var n) ? n : string.Empty;
            d.OrderPayAmount = orderMap.TryGetValue(r.OrderId, out var p) ? p : 0m;
            d.StatusText = RefundStatusHelper.GetName(r.Status);
            d.RefundTypeText = RefundTypeHelper.GetName(r.RefundType);
            return d;
        }).ToList();

        return ApiResponse<PagedResult<RefundListDto>>.Success(
            PagedResult<RefundListDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    // ============================================================
    // 3. 退款详情
    // ============================================================
    public async Task<ApiResponse<RefundDetailDto>> GetDetailAsync(long id)
    {
        var refund = await _refundRepo.GetByIdTrackedAsync(id);
        if (refund == null)
        {
            return ApiResponse<RefundDetailDto>.Fail("退款单不存在", 404);
        }

        var detail = _mapper.Map<RefundDetailDto>(refund);
        detail.StatusText = RefundStatusHelper.GetName(refund.Status);
        detail.RefundTypeText = RefundTypeHelper.GetName(refund.RefundType);

        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == refund.MemberId);
        detail.MemberName = member?.Name ?? string.Empty;

        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == refund.OrderId);
        detail.OrderPayAmount = order?.PayAmount ?? 0m;

        // 关联退货单摘要
        var returns = await _returnRepo.GetByRefundIdAsync(refund.Id);
        detail.Returns = returns.Select(r => new RefundRelatedReturnDto
        {
            Id = r.Id,
            ReturnNo = r.ReturnNo,
            Status = r.Status,
            StatusText = ReturnStatusHelper.GetName(r.Status),
            ReturnAmount = r.ReturnAmount
        }).ToList();

        // 全链路日志
        detail.Logs = await GetLogsAsync(refund.Id);

        return ApiResponse<RefundDetailDto>.Success(detail);
    }

    // ============================================================
    // 4. 审核退款（通过 / 驳回）
    // ============================================================
    public async Task<ApiResponse<object>> ApproveAsync(long id, ApproveRefundDto dto)
    {
        _approveValidator.ValidateAndThrow(dto);

        var refund = await _refundRepo.GetByIdTrackedAsync(id);
        if (refund == null)
        {
            return ApiResponse<object>.Fail("退款单不存在", 404);
        }

        if (refund.Status != (int)RefundStatus.Pending)
        {
            return ApiResponse<object>.Fail("仅待审核的退款单可以审核");
        }

        if (dto.Approved)
        {
            refund.Status = (int)RefundStatus.Approved;
            refund.HandleTime = DateTime.Now;
            refund.OperatorId = _currentUser.UserId;
            refund.AdminRemark = dto.AdminRemark;
            await AppendLogAsync(refund.Id, RefundLogActions.Approved, _currentUser.UserId,
                _currentUser.UserName, dto.AdminRemark);

            // 退货退款：审核通过后自动生成一张"待退货"的退货单（仓库收货后才会真正打款）
            if (refund.RefundType == (int)RefundType.ReturnAndRefund)
            {
                await CreateReturnFromRefundAsync(refund);
            }
            else
            {
                // 仅退款：审核通过即刻完成资金闭环 —— 原路退回会员余额 + 关闭订单。
                // 需求要求「管理员点同意后必须触发退款逻辑并关闭订单」，
                // 因此这里直接复用执行退款核心（幂等：仅 status=1 才会真正打款）。
                await ExecuteRefundCoreAsync(refund, _currentUser.UserId, _currentUser.UserName);
                await CloseOrderAfterRefundAsync(refund);
            }
        }
        else
        {
            refund.Status = (int)RefundStatus.Rejected;
            refund.HandleTime = DateTime.Now;
            refund.OperatorId = _currentUser.UserId;
            refund.AdminRemark = dto.AdminRemark;
            await AppendLogAsync(refund.Id, RefundLogActions.Rejected, _currentUser.UserId,
                _currentUser.UserName, dto.AdminRemark);

            // 驳回：恢复订单原状（回到申请售后之前记录的状态）
            await RestoreOrderStatusAsync(refund);
        }

        await _db.SaveChangesAsync();
        return ApiResponse<object>.Success(new { id }, dto.Approved ? "审核通过" : "已驳回");
    }

    /// <summary>
    /// 审核"退货退款"时，自动生成待退货的退货单（status=1 待退货）。
    /// 退货明细直接取订单明细快照（数量=下单数量），仓库收货时再按实收调整。
    /// </summary>
    private async Task CreateReturnFromRefundAsync(TrxRefund refund)
    {
        // 单号生成 + 唯一索引兜底重试
        TrxReturn? ret = null;
        for (var i = 0; i < MaxNoRetry; i++)
        {
            var candidate = _returnRepo.GenerateReturnNo();
            if (await _returnRepo.ExistsReturnNoAsync(candidate))
            {
                continue;
            }

            var orderItems = await _orderItemRepo.GetListAsync(oi => oi.OrderId == refund.OrderId);

            ret = new TrxReturn
            {
                ReturnNo = candidate,
                OrderId = refund.OrderId,
                RefundId = refund.Id,
                MemberId = refund.MemberId,
                ReturnReason = refund.RefundReason ?? "退货退款",
                ReturnAmount = refund.RefundAmount,
                Status = (int)ReturnStatus.Approved
            };

            // 退货明细：默认退回订单全部商品（数量=下单数量）
            foreach (var oi in orderItems)
            {
                ret.Items.Add(new TrxReturnItem
                {
                    OrderItemId = oi.Id,
                    ProdInfoId = oi.ProdInfoId,
                    SkuId = oi.SkuId,
                    ProdInfoName = oi.ProdName,
                    SkuName = oi.SkuName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.SubtotalAmount,
                    Status = (int)ReturnItemStatus.Pending
                });
            }

            await _returnRepo.AddAsync(ret);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                ret = null;
            }
        }

        if (ret != null)
        {
            await AppendLogAsync(refund.Id, RefundLogActions.ReturnCreated, _currentUser.UserId,
                _currentUser.UserName, $"自动生成退货单 {ret.ReturnNo}");
        }
    }

    // ============================================================
    // 5. 执行退款（财务打款）
    // ============================================================
    public async Task<ApiResponse<object>> ExecuteAsync(long id)
    {
        var refund = await _refundRepo.GetByIdTrackedAsync(id);
        if (refund == null)
        {
            return ApiResponse<object>.Fail("退款单不存在", 404);
        }

        // 幂等：已退款直接返回成功
        if (refund.Status == (int)RefundStatus.Refunded)
        {
            return ApiResponse<object>.Success(new { id }, "该退款单已退款，无需重复执行");
        }

        if (refund.Status != (int)RefundStatus.Approved)
        {
            return ApiResponse<object>.Fail(
                $"仅待退款状态的退款单可执行（当前：{RefundStatusHelper.GetName(refund.Status)}）");
        }

        // 退货退款：必须等仓库收货后才能打款
        if (refund.RefundType == (int)RefundType.ReturnAndRefund)
        {
            var received = await _returnRepo.GetByRefundIdAsync(refund.Id);
            if (!received.Any(r => r.Status == (int)ReturnStatus.Received))
            {
                return ApiResponse<object>.Fail("退货退款需仓库收货后方可执行退款");
            }
        }

        await ExecuteRefundCoreAsync(refund, _currentUser.UserId, _currentUser.UserName);
        await _db.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id }, "退款执行成功");
    }

    /// <summary>
    /// 执行退款核心（改实体 + 写售后日志 + 余额退回，不落库；供内部联动复用）。
    /// 【资金闭环】系统执行「先充值后下单」，订单款从会员余额扣减，
    /// 因此退款成功时必须把金额退回会员可用余额（mkt_balance_log.change_type=3），
    /// 与售后日志在同一事务内提交，保证"退款单状态"与"会员余额"永不脱节。
    /// </summary>
    public async Task ExecuteRefundCoreAsync(TrxRefund refund, long operatorId, string operatorName)
    {
        refund.Status = (int)RefundStatus.Refunded;
        refund.RefundTime = DateTime.Now;
        refund.OperatorId = operatorId;
        // 日志在调用方 SaveChanges 前加入（此处直接加实体，随主事务一起提交）
        await _logRepo.AddAsync(new TrxRefundLog
        {
            RefundId = refund.Id,
            Action = RefundLogActions.Refunded,
            OperatorId = operatorId,
            OperatorName = operatorName,
            Remark = "执行退款（财务打款）",
            CreateTime = DateTime.Now
        });

        // 退款金额退回会员可用余额（幂等由调用方的状态机保证：仅 status=1 才可执行）
        if (refund.RefundAmount > 0)
        {
            await _balanceService.RefundBackAsync(
                refund.MemberId, refund.RefundAmount, refund.Id, $"退款单 {refund.RefundNo} 退回余额");
        }
    }

    // ============================================================
    // 6. 会员端（小程序）自助申请售后
    // ============================================================

    /// <summary>
    /// 会员自助申请售后（POST /api/mp/after-sales/apply）。
    /// 相比后台 CreateAsync 放宽了订单状态：已支付(1)/已发货(2)/已完成(3) 均可申请
    /// （后台仅限已完成，因为后台是代客操作；会员自助需覆盖"未发货/未收货就想退"的场景）。
    /// 安全：memberId 由控制器从 JWT 解析传入，且强制校验 order.BuyerId == memberId，防越权。
    /// 副作用：订单状态置为 5-售后中，并把原状态写入 original_order_status 供驳回恢复。
    /// </summary>
    public async Task<ApiResponse<RefundCreateResult>> ApplyByMemberAsync(MpApplyRefundDto dto, long memberId)
    {
        // 字段级校验（[Required]/[Range] 等）由 [ApiController] 在进入 Service 前已完成，
        // 这里只做业务规则校验。
        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.IsDeleted == 0);
        if (order == null)
        {
            return ApiResponse<RefundCreateResult>.Fail("订单不存在", 404);
        }

        // 越权防护：订单不属于当前会员时，按"不存在"返回，避免泄露他人订单信息
        if (order.BuyerId != memberId)
        {
            return ApiResponse<RefundCreateResult>.Fail("订单不存在", 404);
        }

        // 可申请售后的订单状态：1-已支付(待发货) 2-已发货(待收货) 3-已完成
        if (order.OrderStatus != OrderStatuses.Paid &&
            order.OrderStatus != OrderStatuses.Shipped &&
            order.OrderStatus != OrderStatuses.Completed)
        {
            return ApiResponse<RefundCreateResult>.Fail(
                $"当前订单状态（{OrderStatuses.GetName(order.OrderStatus)}）不支持申请售后");
        }

        // 同一订单不允许重复提交进行中的售后单（0-待审核 / 1-待退款）
        var hasPending = await _db.TrxRefunds.AnyAsync(r =>
            r.OrderId == order.Id &&
            (r.Status == (int)RefundStatus.Pending || r.Status == (int)RefundStatus.Approved));
        if (hasPending)
        {
            return ApiResponse<RefundCreateResult>.Fail("该订单已有进行中的售后申请，请勿重复提交");
        }

        // 金额上限：订单实付 - 已占用（待退款 + 已退款）
        var refundedSum = await _refundRepo.GetRefundedSumByOrderAsync(order.Id);
        var available = order.PayAmount - refundedSum;
        if (dto.RefundAmount > available)
        {
            return ApiResponse<RefundCreateResult>.Fail(
                $"退款金额超出可退额度（订单实付 {order.PayAmount:0.00}，已申请 {refundedSum:0.00}，可退 {available:0.00}）");
        }

        // 单号生成 + 唯一索引兜底重试
        TrxRefund? refund = null;
        for (var i = 0; i < MaxNoRetry; i++)
        {
            var candidate = _refundRepo.GenerateRefundNo();
            if (await _refundRepo.ExistsRefundNoAsync(candidate))
            {
                continue;
            }

            refund = new TrxRefund
            {
                RefundNo = candidate,
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                // 记下申请前的订单状态，供审核驳回时恢复原状
                OriginalOrderStatus = order.OrderStatus,
                Source = 2, // 2-小程序会员自助
                MemberId = memberId,
                RefundAmount = dto.RefundAmount,
                RefundType = dto.RefundType,
                RefundReason = dto.RefundReason,
                VoucherImages = dto.VoucherImages,
                Status = (int)RefundStatus.Pending
            };

            await _refundRepo.AddAsync(refund);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                refund = null;
            }
        }

        if (refund == null)
        {
            return ApiResponse<RefundCreateResult>.Fail("生成退款单号失败，请稍后重试");
        }

        // 订单进入「售后中」，并留痕（与售后单在同一 SaveChanges 内提交）
        order.OrderStatus = OrderStatuses.AfterSale;
        await AppendLogAsync(refund.Id, RefundLogActions.Created, memberId, "会员自助",
            $"小程序提交售后申请（{RefundTypeHelper.GetName(refund.RefundType)}）");
        await _db.SaveChangesAsync();

        return ApiResponse<RefundCreateResult>.Success(
            new RefundCreateResult { Id = refund.Id, RefundNo = refund.RefundNo },
            "售后申请已提交，等待审核");
    }

    /// <summary>
    /// 会员售后列表（GET /api/mp/after-sales/list）。
    /// 强制按 memberId 过滤（绝不信任前端参数），并按订单明细补全商品摘要，
    /// 让小程序「退款/售后」Tab 的卡片能直接展示商品图 + 名称 + 状态标签。
    /// </summary>
    public async Task<ApiResponse<PagedResult<MpRefundListItemDto>>> GetMyListAsync(long memberId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 50 ? 50 : pageSize);

        var query = _db.TrxRefunds.AsNoTracking().Where(r => r.MemberId == memberId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orderIds = items.Select(r => r.OrderId).Distinct().ToList();

        // 批量补全订单与明细，避免 N+1
        var orderMap = orderIds.Count == 0
            ? new Dictionary<long, TrxOrder>()
            : (await _orderRepo.GetListAsync(o => orderIds.Contains(o.Id))).ToDictionary(o => o.Id);
        var orderItems = orderIds.Count == 0
            ? new List<TrxOrderItem>()
            : await _orderItemRepo.GetListAsync(oi => orderIds.Contains(oi.OrderId));

        var dtos = items.Select(r =>
        {
            orderMap.TryGetValue(r.OrderId, out var order);
            return new MpRefundListItemDto
            {
                Id = r.Id,
                RefundNo = r.RefundNo,
                OrderId = r.OrderId,
                OrderNo = r.OrderNo,
                RefundAmount = r.RefundAmount,
                RefundType = r.RefundType,
                RefundTypeText = RefundTypeHelper.GetName(r.RefundType),
                RefundReason = r.RefundReason,
                VoucherImages = r.VoucherImages,
                Status = r.Status,
                StatusText = GetMpStatusText(r.Status),
                OrderStatus = order?.OrderStatus ?? 0,
                OrderPayAmount = order?.PayAmount ?? 0m,
                AdminRemark = r.AdminRemark,
                HandleTime = r.HandleTime,
                RefundTime = r.RefundTime,
                CreateTime = r.CreateTime,
                Items = orderItems
                    .Where(oi => oi.OrderId == r.OrderId)
                    .Take(3)
                    .Select(oi => new MpRefundOrderItemDto
                    {
                        ProdName = oi.ProdName,
                        SkuName = oi.SkuName,
                        Image = oi.ProductImage,
                        Quantity = oi.Quantity,
                        Price = oi.UnitPrice
                    })
                    .ToList()
            };
        }).ToList();

        return ApiResponse<PagedResult<MpRefundListItemDto>>.Success(
            PagedResult<MpRefundListItemDto>.Create(dtos, total, page, pageSize));
    }

    /// <summary>小程序口径的售后状态文案（比后台文案更贴近消费者视角）</summary>
    private static string GetMpStatusText(int status) => status switch
    {
        (int)RefundStatus.Pending => "售后审核中",
        (int)RefundStatus.Approved => "退款中",
        (int)RefundStatus.Refunded => "退款成功",
        (int)RefundStatus.Rejected => "售后关闭",
        _ => "未知"
    };

    /// <summary>仅退款审核通过后关闭订单（钱已退回，订单终止）</summary>
    private async Task CloseOrderAfterRefundAsync(TrxRefund refund)
    {
        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == refund.OrderId);
        if (order == null)
        {
            return;
        }
        order.OrderStatus = OrderStatuses.Closed;
    }

    /// <summary>审核驳回后恢复订单原状（仅当订单仍处于"售后中"时才回滚，避免并发误改）</summary>
    private async Task RestoreOrderStatusAsync(TrxRefund refund)
    {
        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == refund.OrderId);
        if (order == null || order.OrderStatus != OrderStatuses.AfterSale)
        {
            return;
        }
        // 优先用申请时记录的原状态；老数据无记录时兜底回"已完成"
        order.OrderStatus = refund.OriginalOrderStatus ?? OrderStatuses.Completed;
    }

    // ============================================================
    // 私有：日志写入
    // ============================================================
    private async Task AppendLogAsync(long refundId, string action, long? operatorId, string operatorName, string? remark)
    {
        await _logRepo.AddAsync(new TrxRefundLog
        {
            RefundId = refundId,
            Action = action,
            OperatorId = operatorId,
            OperatorName = operatorName,
            Remark = remark,
            CreateTime = DateTime.Now
        });
    }

    /// <summary>按退款单查询全链路日志（按时间正序，便于前端展示轨迹）</summary>
    internal async Task<List<RefundLogDto>> GetLogsAsync(long refundId)
    {
        var logs = await _logRepo.GetListAsync(l => l.RefundId == refundId);
        return logs.OrderBy(l => l.CreateTime).Select(l => new RefundLogDto
        {
            Id = l.Id,
            RefundId = l.RefundId,
            Action = l.Action,
            ActionText = RefundLogActionHelper.GetName(l.Action),
            OperatorId = l.OperatorId,
            OperatorName = l.OperatorName,
            Remark = l.Remark,
            CreateTime = l.CreateTime
        }).ToList();
    }
}
