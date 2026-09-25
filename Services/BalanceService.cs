using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 会员余额服务实现（B组：充值与余额）
///
/// ┌──────────────────────────────────────────────────────────────────────┐
/// │ 一、资金安全铁律                                                                                              │
/// │   1. 所有余额变动（充值/冻结/解冻/扣减/调整）必须在【数据库事务】内完成；                                              │
/// │   2. 每次变动必须写 mkt_balance_log，约定：                                                                       │
/// │        after_balance 恒等于「操作后 mem_member.balance」，作为对账真值；                                          │
/// │        change_amount 一律记正数，方向由 change_type 语义决定；                                                     │
/// │   3. 金额字段一律 decimal(10,2)，禁用 double / float；折扣率 decimal(5,4)；                                       │
/// │   4. 冻结/扣减/调整/入账全部走【CAS 乐观锁】：                                                                     │
/// │        UPDATE mem_member SET ... WHERE id=@id AND balance=@before AND frozen_balance=@beforeFrozen                │
/// │        受影响行数=0 说明被并发修改，重读后重试（最多 MaxCasRetry 次），彻底杜绝超扣。                                  │
/// ├──────────────────────────────────────────────────────────────────────┤
/// │ 二、幂等                                                                                                      │
/// │   微信回调基于 transaction_id 判重（该列唯一索引兜底）：已入账直接返回成功，严禁重复入账。                                │
/// ├──────────────────────────────────────────────────────────────────────┤
/// │ 三、业务流转（先充值后下单）                                                                                       │
/// │   校验余额 → 冻结(balance→frozen) → 创建订单 → 写价格快照 → 支付扣减(frozen 销账)                                   │
/// │   取消/退款：解冻(frozen→balance) → 退款退回(balance 增加)                                                        │
/// └──────────────────────────────────────────────────────────────────────┘
/// </summary>
public class BalanceService : IBalanceService
{
    /// <summary>CAS 乐观锁最大重试次数</summary>
    private const int MaxCasRetry = 3;

    /// <summary>
    /// 关联单据类型（mkt_balance_log.related_type）
    /// ⚠️ 数据库该列是 TINYINT（0-无 1-订单 2-充值订单 3-退款单），见 Jsd_order.sql，
    ///    不是字符串；后台调整没有独立取值，记 0-无（备注里写明原因）。
    /// </summary>
    private const int RelatedRecharge = (int)BalanceRelatedType.Recharge;
    private const int RelatedOrder = (int)BalanceRelatedType.Order;
    private const int RelatedRefund = (int)BalanceRelatedType.Refund;
    private const int RelatedAdjust = (int)BalanceRelatedType.None;

    private readonly AppDbContext _db;
    private readonly IMemRechargeRepository _rechargeRepo;
    private readonly IBalanceLogRepository _logRepo;
    private readonly IRepository<MemMember> _memberRepo;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly RechargeCreateValidator _rechargeValidator;
    private readonly AdminAdjustBalanceValidator _adjustValidator;
    private readonly FreezeBalanceValidator _freezeValidator;
    private readonly UnfreezeBalanceValidator _unfreezeValidator;
    private readonly DeductBalanceValidator _deductValidator;

    public BalanceService(
        AppDbContext db,
        IMemRechargeRepository rechargeRepo,
        IBalanceLogRepository logRepo,
        IRepository<MemMember> memberRepo,
        IMapper mapper,
        CurrentUserService currentUser,
        RechargeCreateValidator rechargeValidator,
        AdminAdjustBalanceValidator adjustValidator,
        FreezeBalanceValidator freezeValidator,
        UnfreezeBalanceValidator unfreezeValidator,
        DeductBalanceValidator deductValidator)
    {
        _db = db;
        _rechargeRepo = rechargeRepo;
        _logRepo = logRepo;
        _memberRepo = memberRepo;
        _mapper = mapper;
        _currentUser = currentUser;
        _rechargeValidator = rechargeValidator;
        _adjustValidator = adjustValidator;
        _freezeValidator = freezeValidator;
        _unfreezeValidator = unfreezeValidator;
        _deductValidator = deductValidator;
    }

    // ============================================================
    // 一、充值
    // ============================================================

    /// <summary>
    /// 创建充值订单：生成唯一 recharge_no，状态=待支付，返回拉起支付所需的参数。
    /// 说明：本项目未接入微信支付 SDK，PrepayId/CodeUrl 为占位；
    ///       接入后只需替换返回值内容，接口契约与 Service 逻辑不变。
    /// </summary>
    public async Task<ApiResponse<RechargePayParamsDto>> CreateRechargeAsync(RechargeCreateDto dto)
    {
        _rechargeValidator.ValidateAndThrow(dto);

        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == dto.MemMemberId && m.IsDeleted == 0);
        if (member == null)
        {
            return ApiResponse<RechargePayParamsDto>.Fail("会员不存在", 404);
        }
        if (member.Status != 1)
        {
            return ApiResponse<RechargePayParamsDto>.Fail("会员已禁用，无法充值");
        }

        // 单号生成 + 唯一索引兜底重试
        MemRecharge? entity = null;
        for (var i = 0; i < 5; i++)
        {
            var candidate = _rechargeRepo.GenerateRechargeNo();
            if (await _rechargeRepo.ExistsRechargeNoAsync(candidate))
            {
                continue;
            }

            entity = new MemRecharge
            {
                RechargeNo = candidate,
                MemMemberId = dto.MemMemberId,
                RechargeAmount = decimal.Round(dto.RechargeAmount, 2),
                GiftAmount = decimal.Round(dto.GiftAmount, 2),
                // 落库用数值枚举（pay_type / recharge_channel），渠道码字符串只在 DTO 层对外暴露
                PayType = PayTypeHelper.FromCode(dto.PayChannel),
                RechargeChannel = (int)RechargeChannel.Self,
                Status = (int)RechargeStatus.Pending,
                OperatorId = null
            };

            await _rechargeRepo.AddAsync(entity);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                entity = null;
            }
        }

        if (entity == null)
        {
            return ApiResponse<RechargePayParamsDto>.Fail("充值单号生成失败，请稍后重试");
        }

        var result = new RechargePayParamsDto
        {
            RechargeId = entity.Id,
            RechargeNo = entity.RechargeNo,
            PayAmount = entity.RechargeAmount,
            GiftAmount = entity.GiftAmount,
            PayChannel = PayTypeHelper.GetCode(entity.PayType),
            CodeUrl = null,     // 接入微信 Native 支付后填充 code_url
            PrepayId = null,    // 接入微信 JSAPI 支付后填充 prepay_id
            Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        };

        return ApiResponse<RechargePayParamsDto>.Success(result, "充值单已创建，请完成支付");
    }

    /// <summary>
    /// 微信支付回调：幂等 + 事务入账。
    /// 入账内容：status→已支付、pay_time 写入、balance += 本金+赠送、total_recharge += 本金、写余额流水。
    /// 重复回调（同一 transaction_id）直接返回成功，绝不做二次入账。
    /// </summary>
    public async Task<WechatNotifyResultDto> HandleWechatNotifyAsync(WechatNotifyDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.OutTradeNo))
        {
            return new WechatNotifyResultDto { Success = false, Message = "回调参数无效：缺少商户订单号" };
        }

        var outTradeNo = dto.OutTradeNo.Trim();

        // ---- 1) 幂等第一道：按微信流水号判重（transaction_id 唯一索引兜底）----
        if (!string.IsNullOrWhiteSpace(dto.TransactionId))
        {
            var byTx = await _rechargeRepo.GetByTransactionIdAsync(dto.TransactionId.Trim());
            if (byTx != null && byTx.Status == (int)RechargeStatus.Paid)
            {
                var balance = await GetAvailableBalanceAsync(byTx.MemMemberId);
                return new WechatNotifyResultDto
                {
                    Success = true,
                    Duplicated = true,
                    RechargeNo = byTx.RechargeNo,
                    Balance = balance,
                    Message = "重复回调已幂等拦截（未重复入账）"
                };
            }
        }

        // ---- 2) 定位充值单 ----
        var recharge = await _rechargeRepo.GetByRechargeNoTrackedAsync(outTradeNo);
        if (recharge == null)
        {
            return new WechatNotifyResultDto { Success = false, Message = $"充值单不存在（{outTradeNo}）" };
        }

        // ---- 3) 幂等第二道：本单已支付 ----
        if (recharge.Status == (int)RechargeStatus.Paid)
        {
            var balance = await GetAvailableBalanceAsync(recharge.MemMemberId);
            return new WechatNotifyResultDto
            {
                Success = true,
                Duplicated = true,
                RechargeNo = recharge.RechargeNo,
                Balance = balance,
                Message = "该充值单已入账，重复回调已忽略"
            };
        }

        // ---- 4) 业务结果非成功：标记为「已关闭」(pay_status=2)，不入账 ----
        //     数据库语义 0待支付 1已到账 2已关闭 3已退款，没有独立的"失败"值，失败归入已关闭。
        if (!string.Equals(dto.ResultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            recharge.Status = (int)RechargeStatus.Closed;
            await _db.SaveChangesAsync();
            return new WechatNotifyResultDto
            {
                Success = false,
                RechargeNo = recharge.RechargeNo,
                Message = $"支付失败（resultCode={dto.ResultCode}）"
            };
        }

        // ---- 5) 金额校验：回调金额必须与充值单本金一致 ----
        if (dto.Amount.HasValue && decimal.Round(dto.Amount.Value, 2) != recharge.RechargeAmount)
        {
            return new WechatNotifyResultDto
            {
                Success = false,
                RechargeNo = recharge.RechargeNo,
                Message = $"回调金额与充值单金额不一致（回调 {dto.Amount:0.00}，单据 {recharge.RechargeAmount:0.00}）"
            };
        }

        // ---- 6) 事务入账 ----
        var now = DateTime.Now;
        var credit = recharge.RechargeAmount + recharge.GiftAmount;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            recharge.Status = (int)RechargeStatus.Paid;
            recharge.PayTime = now;
            recharge.TransactionId = dto.TransactionId?.Trim();

            var after = await CreditAsync(
                recharge.MemMemberId, credit, recharge.RechargeAmount,
                BalanceChangeType.Recharge, RelatedRecharge, recharge.Id,
                $"充值入账：本金 {recharge.RechargeAmount:0.00} + 赠送 {recharge.GiftAmount:0.00}（{recharge.RechargeNo}）",
                null, now);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new WechatNotifyResultDto
            {
                Success = true,
                Duplicated = false,
                RechargeNo = recharge.RechargeNo,
                Balance = after,
                Message = "充值入账成功"
            };
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return new WechatNotifyResultDto { Success = false, RechargeNo = recharge.RechargeNo, Message = ex.Message };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>分页查询充值记录（补全客户名、状态文案、到账金额）</summary>
    public async Task<ApiResponse<PagedResult<RechargeDto>>> GetRechargesAsync(RechargeQueryDto q)
    {
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1 || q.PageSize > 100) q.PageSize = 10;

        var (items, total) = await _rechargeRepo.GetPagedListAsync(q);
        var dtos = await MapRechargeDtosAsync(items, false);

        return ApiResponse<PagedResult<RechargeDto>>.Success(
            PagedResult<RechargeDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    /// <summary>充值单详情（含支付回调结果、微信交易号与会员当前余额）</summary>
    public async Task<ApiResponse<RechargeDto>> GetRechargeDetailAsync(long id)
    {
        var entity = await _rechargeRepo.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse<RechargeDto>.Fail("充值单不存在", 404);
        }

        var dto = (await MapRechargeDtosAsync(new List<MemRecharge> { entity }, true)).First();
        return ApiResponse<RechargeDto>.Success(dto);
    }

    /// <summary>
    /// 后台代客充值：线下转账/现金收款确认后由运营发起，【直接入账】不走支付回调。
    /// 事务内完成：建单（已支付）+ 余额/累计充值累加 + 写流水，任一环节失败整体回滚。
    /// </summary>
    public async Task<ApiResponse<RechargeDto>> AdminRechargeAsync(AdminRechargeDto dto)
    {
        if (dto.MemMemberId <= 0)
        {
            return ApiResponse<RechargeDto>.Fail("请选择充值客户");
        }
        if (dto.RechargeAmount <= 0)
        {
            return ApiResponse<RechargeDto>.Fail("充值金额必须大于 0");
        }
        if (dto.GiftAmount < 0)
        {
            return ApiResponse<RechargeDto>.Fail("赠送金额不能为负数");
        }

        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == dto.MemMemberId && m.IsDeleted == 0);
        if (member == null)
        {
            return ApiResponse<RechargeDto>.Fail("会员不存在", 404);
        }

        // 线下流水号唯一性预检（transaction_id 唯一索引兜底）
        var txId = string.IsNullOrWhiteSpace(dto.TransactionId) ? null : dto.TransactionId.Trim();
        if (txId != null && await _rechargeRepo.GetByTransactionIdAsync(txId) != null)
        {
            return ApiResponse<RechargeDto>.Fail($"线下流水号 {txId} 已存在，请勿重复入账");
        }

        var now = DateTime.Now;
        MemRecharge? entity = null;
        for (var i = 0; i < 5; i++)
        {
            var candidate = _rechargeRepo.GenerateRechargeNo();
            if (await _rechargeRepo.ExistsRechargeNoAsync(candidate))
            {
                continue;
            }

            entity = new MemRecharge
            {
                RechargeNo = candidate,
                MemMemberId = dto.MemMemberId,
                RechargeAmount = decimal.Round(dto.RechargeAmount, 2),
                GiftAmount = decimal.Round(dto.GiftAmount, 2),
                PayType = (int)PayType.Offline,                 // 线下转账
                RechargeChannel = (int)RechargeChannel.Admin,   // 后台代充
                TransactionId = txId,
                Status = (int)RechargeStatus.Pending,
                OperatorId = _currentUser.UserId,
                Remark = dto.Remark?.Trim()
            };

            await _rechargeRepo.AddAsync(entity);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                entity = null;
            }
        }

        if (entity == null)
        {
            return ApiResponse<RechargeDto>.Fail("充值单号生成失败，请稍后重试");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            entity.Status = (int)RechargeStatus.Paid;
            entity.PayTime = now;

            await CreditAsync(
                entity.MemMemberId, entity.RechargeAmount + entity.GiftAmount, entity.RechargeAmount,
                BalanceChangeType.Recharge, RelatedRecharge, entity.Id,
                $"后台代客充值：本金 {entity.RechargeAmount:0.00} + 赠送 {entity.GiftAmount:0.00}（{entity.RechargeNo}）{dto.Remark}",
                _currentUser.UserId, now);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse<RechargeDto>.Fail(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var result = (await MapRechargeDtosAsync(new List<MemRecharge> { entity }, true)).First();
        return ApiResponse<RechargeDto>.Success(result, "代客充值成功，余额已到账");
    }

    /// <summary>
    /// 充值退款（仅已支付可退）：
    /// 事务内 充值单状态 → 已退款，并从会员可用余额【扣回】本金+赠送（赠送一并收回），写后台调整流水。
    /// 余额不足时拒绝退款并给出明确提示（说明该客户已把钱用于下单）。
    /// </summary>
    public async Task<ApiResponse<bool>> RefundRechargeAsync(long id, RechargeRefundDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return ApiResponse<bool>.Fail("退款原因必填（资金操作必须可审计）");
        }

        var entity = await _db.MemRecharges.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("充值单不存在", 404);
        }
        if (entity.Status != (int)RechargeStatus.Paid)
        {
            return ApiResponse<bool>.Fail($"仅已支付的充值单可退款（当前：{RechargeStatusHelper.GetName(entity.Status)}）");
        }

        var now = DateTime.Now;
        var back = entity.RechargeAmount + entity.GiftAmount;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            entity.Status = (int)RechargeStatus.Refunded;

            await DebitAvailableAsync(
                entity.MemMemberId, back, BalanceChangeType.AdminAdjust, RelatedRecharge, entity.Id,
                $"充值退款扣回：本金 {entity.RechargeAmount:0.00} + 赠送 {entity.GiftAmount:0.00}（{entity.RechargeNo}）原因：{dto.Reason}",
                _currentUser.UserId, now);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<bool>.Success(true, "充值退款成功，余额已扣回");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse<bool>.Fail(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 二、余额查询
    // ============================================================

    /// <summary>查询会员余额概况（可用 / 冻结 / 累计充值 / 等级）</summary>
    public async Task<ApiResponse<MemberBalanceDto>> GetMemberBalanceAsync(long memMemberId)
    {
        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == memMemberId);
        if (member == null)
        {
            return ApiResponse<MemberBalanceDto>.Fail("会员不存在", 404);
        }

        string? levelName = null;
        if (member.CustomerLevelId.HasValue)
        {
            levelName = await _db.MemMemberLevels.AsNoTracking()
                .Where(l => l.Id == member.CustomerLevelId.Value)
                .Select(l => l.LevelName)
                .FirstOrDefaultAsync();
        }

        return ApiResponse<MemberBalanceDto>.Success(new MemberBalanceDto
        {
            MemMemberId = member.Id,
            MemMemberName = member.Name,
            MemberNo = member.MemberNo,
            Balance = member.Balance,
            FrozenBalance = member.FrozenBalance,
            TotalRecharge = member.TotalRecharge,
            CustomerLevelId = member.CustomerLevelId,
            CustomerLevelName = levelName
        });
    }

    /// <summary>分页查询余额流水（补全会员名、变动类型文案、操作人）</summary>
    public async Task<ApiResponse<PagedResult<BalanceLogDto>>> GetLogsAsync(BalanceLogQueryDto q)
    {
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1 || q.PageSize > 100) q.PageSize = 10;

        var (items, total) = await _logRepo.GetPagedListAsync(q);

        var memberIds = items.Select(l => l.MemMemberId).Distinct().ToList();
        var memberMap = memberIds.Count == 0
            ? new Dictionary<long, MemMember>()
            : await _db.MemMembers.AsNoTracking()
                .Where(m => memberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

        var userMap = await GetUserNameMapAsync(
            items.Select(l => l.OperatorId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList());

        var dtos = items.Select(l =>
        {
            var d = _mapper.Map<BalanceLogDto>(l);
            d.ChangeTypeText = BalanceChangeTypeHelper.GetName(l.ChangeType);
            if (memberMap.TryGetValue(l.MemMemberId, out var m))
            {
                d.MemMemberName = m.Name;
                d.MemberNo = m.MemberNo;
            }
            d.OperatorName = l.OperatorId.HasValue && userMap.TryGetValue(l.OperatorId.Value, out var un) ? un : null;
            return d;
        }).ToList();

        return ApiResponse<PagedResult<BalanceLogDto>>.Success(
            PagedResult<BalanceLogDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    // ============================================================
    // 三、余额操作（公开接口：返回 ApiResponse）
    // ============================================================

    /// <summary>后台手工调整余额（可正可负，必须填原因，记录 operator_id）</summary>
    public async Task<ApiResponse<bool>> AdminAdjustAsync(AdminAdjustBalanceDto dto)
    {
        _adjustValidator.ValidateAndThrow(dto);

        var amount = decimal.Round(dto.Amount, 2);
        var now = DateTime.Now;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (amount > 0)
            {
                await CreditAsync(dto.MemMemberId, amount, null, BalanceChangeType.AdminAdjust,
                    RelatedAdjust, null, $"后台调整（增加）：{dto.Reason}", _currentUser.UserId, now);
            }
            else
            {
                await DebitAvailableAsync(dto.MemMemberId, -amount, BalanceChangeType.AdminAdjust,
                    RelatedAdjust, null, $"后台调整（扣减）：{dto.Reason}", _currentUser.UserId, now);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<bool>.Success(true, "余额调整成功");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse<bool>.Fail(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>下单冻结：校验 balance ≥ 金额，balance -= 金额，frozen_balance += 金额</summary>
    public async Task<ApiResponse<bool>> FreezeAsync(FreezeBalanceDto dto)
    {
        _freezeValidator.ValidateAndThrow(dto);

        try
        {
            await FreezeForOrderAsync(dto.MemMemberId, decimal.Round(dto.Amount, 2),
                dto.RelatedId ?? 0, dto.Remark, dto.RelatedType);
            await _db.SaveChangesAsync();
            return ApiResponse<bool>.Success(true, "余额冻结成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
    }

    /// <summary>取消/退款解冻：frozen_balance -= 金额，balance += 金额</summary>
    public async Task<ApiResponse<bool>> UnfreezeAsync(UnfreezeBalanceDto dto)
    {
        _unfreezeValidator.ValidateAndThrow(dto);

        try
        {
            await UnfreezeForOrderAsync(dto.MemMemberId, decimal.Round(dto.Amount, 2),
                dto.RelatedId ?? 0, dto.Remark, dto.RelatedType);
            await _db.SaveChangesAsync();
            return ApiResponse<bool>.Success(true, "余额解冻成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
    }

    /// <summary>下单扣减：frozen_balance -= 金额（资金真正划走），写扣减流水</summary>
    public async Task<ApiResponse<bool>> DeductAsync(DeductBalanceDto dto)
    {
        _deductValidator.ValidateAndThrow(dto);

        try
        {
            await DeductForOrderAsync(dto.MemMemberId, decimal.Round(dto.Amount, 2),
                dto.RelatedId ?? 0, dto.Remark, dto.RelatedType);
            await _db.SaveChangesAsync();
            return ApiResponse<bool>.Success(true, "余额扣减成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
    }

    /// <summary>「先充值后下单」的下单前余额校验入口</summary>
    public async Task<ApiResponse<bool>> CheckEnoughAsync(long memMemberId, decimal amount)
    {
        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == memMemberId);
        if (member == null)
        {
            return ApiResponse<bool>.Fail("会员不存在");
        }

        var need = decimal.Round(amount, 2);
        if (need <= 0)
        {
            return ApiResponse<bool>.Success(true);
        }

        if (member.Balance < need)
        {
            return ApiResponse<bool>.Fail(
                $"余额不足：当前可用余额 {member.Balance:0.00} 元，本次需支付 {need:0.00} 元，请先充值后再下单");
        }

        return ApiResponse<bool>.Success(true);
    }

    // ============================================================
    // 四、供订单 / 售后模块内部复用（失败直接抛业务异常，由调用方事务回滚）
    // ============================================================

    /// <summary>订单冻结：可用余额 → 冻结余额（不落库，随调用方事务提交）</summary>
    public async Task FreezeForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null)
    {
        var amt = decimal.Round(amount, 2);
        if (amt <= 0)
        {
            return;
        }

        var member = await RequireMemberAsync(memMemberId);

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var before = member.Balance;
            var beforeFrozen = member.FrozenBalance;

            if (before < amt)
            {
                throw new InvalidOperationException(
                    $"余额不足：当前可用余额 {before:0.00} 元，本次需冻结 {amt:0.00} 元，请先充值后再下单");
            }

            var afterBalance = before - amt;
            var afterFrozen = beforeFrozen + amt;

            var ok = await CasUpdateAsync(member.Id, before, beforeFrozen, afterBalance, afterFrozen, null);
            if (!ok)
            {
                await _db.Entry(member).ReloadAsync();   // 并发冲突：重读最新值重试
                continue;
            }

            await AppendLogAsync(member.Id, BalanceChangeType.Freeze, amt, before, afterBalance,
                relatedType ?? RelatedOrder, orderId, remark ?? $"下单冻结 {amt:0.00} 元（订单 {orderId}）", null, DateTime.Now);
            return;
        }

        throw new InvalidOperationException("余额冻结并发冲突，请重试");
    }

    /// <summary>订单支付成功后扣减冻结额（不落库，随调用方事务提交）</summary>
    public async Task DeductForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null)
    {
        var amt = decimal.Round(amount, 2);
        if (amt <= 0)
        {
            return;
        }

        var member = await RequireMemberAsync(memMemberId);

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var before = member.Balance;          // 扣减只动冻结额，可用余额不变
            var beforeFrozen = member.FrozenBalance;

            if (beforeFrozen < amt)
            {
                throw new InvalidOperationException(
                    $"冻结余额不足：当前冻结 {beforeFrozen:0.00} 元，本次需扣减 {amt:0.00} 元（疑似重复扣减）");
            }

            var afterFrozen = beforeFrozen - amt;

            var ok = await CasUpdateAsync(member.Id, before, beforeFrozen, before, afterFrozen, null);
            if (!ok)
            {
                await _db.Entry(member).ReloadAsync();
                continue;
            }

            await AppendLogAsync(member.Id, BalanceChangeType.OrderDeduct, amt, before, before,
                relatedType ?? RelatedOrder, orderId, remark ?? $"订单支付扣减 {amt:0.00} 元（订单 {orderId}）", null, DateTime.Now);
            return;
        }

        throw new InvalidOperationException("余额扣减并发冲突，请重试");
    }

    /// <summary>订单取消 / 关闭后解冻：冻结额 → 可用余额（不落库，随调用方事务提交）</summary>
    public async Task UnfreezeForOrderAsync(long memMemberId, decimal amount, long orderId, string? remark = null, int? relatedType = null)
    {
        var amt = decimal.Round(amount, 2);
        if (amt <= 0)
        {
            return;
        }

        var member = await RequireMemberAsync(memMemberId);

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var before = member.Balance;
            var beforeFrozen = member.FrozenBalance;

            // 解冻金额以"实际冻结额"为上限，避免超额退回（如部分已扣减后取消）
            var real = amt > beforeFrozen ? beforeFrozen : amt;
            if (real <= 0)
            {
                return;
            }

            var afterBalance = before + real;
            var afterFrozen = beforeFrozen - real;

            var ok = await CasUpdateAsync(member.Id, before, beforeFrozen, afterBalance, afterFrozen, null);
            if (!ok)
            {
                await _db.Entry(member).ReloadAsync();
                continue;
            }

            await AppendLogAsync(member.Id, BalanceChangeType.Unfreeze, real, before, afterBalance,
                relatedType ?? RelatedOrder, orderId, remark ?? $"订单取消解冻退回 {real:0.00} 元（订单 {orderId}）", null, DateTime.Now);
            return;
        }

        throw new InvalidOperationException("余额解冻并发冲突，请重试");
    }

    /// <summary>退款执行成功后把金额退回会员可用余额（change_type=3，不落库）</summary>
    public async Task RefundBackAsync(long memMemberId, decimal amount, long refundId, string? remark = null)
    {
        var amt = decimal.Round(amount, 2);
        if (amt <= 0)
        {
            return;
        }

        await CreditAsync(memMemberId, amt, null, BalanceChangeType.RefundBack, RelatedRefund, refundId,
            remark ?? $"退款退回 {amt:0.00} 元（退款单 {refundId}）", _currentUser.UserId, DateTime.Now);
    }

    // ============================================================
    // 私有：资金内核
    // ============================================================

    /// <summary>
    /// 入账（可用余额增加）：CAS 更新 + 写流水。
    /// </summary>
    /// <param name="memberId">会员ID</param>
    /// <param name="amount">入账金额（本金+赠送）</param>
    /// <param name="principal">计入累计充值的本金（不传则不累加 total_recharge）</param>
    private async Task<decimal> CreditAsync(
        long memberId, decimal amount, decimal? principal,
        BalanceChangeType changeType, int relatedType, long? relatedId,
        string remark, long? operatorId, DateTime now)
    {
        var member = await RequireMemberAsync(memberId);
        var amt = decimal.Round(amount, 2);

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var before = member.Balance;
            var beforeFrozen = member.FrozenBalance;
            var afterBalance = before + amt;
            var newTotal = principal.HasValue
                ? member.TotalRecharge + decimal.Round(principal.Value, 2)
                : (decimal?)null;

            var ok = await CasUpdateAsync(member.Id, before, beforeFrozen, afterBalance, beforeFrozen, newTotal);
            if (!ok)
            {
                await _db.Entry(member).ReloadAsync();
                continue;
            }

            await AppendLogAsync(member.Id, changeType, amt, before, afterBalance,
                relatedType, relatedId, remark, operatorId, now);
            return afterBalance;
        }

        throw new InvalidOperationException("余额入账并发冲突，请重试");
    }

    /// <summary>扣减可用余额（后台调整减款场景）：CAS 更新 + 写流水</summary>
    private async Task<decimal> DebitAvailableAsync(
        long memberId, decimal amount,
        BalanceChangeType changeType, int relatedType, long? relatedId,
        string remark, long? operatorId, DateTime now)
    {
        var member = await RequireMemberAsync(memberId);
        var amt = decimal.Round(amount, 2);

        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var before = member.Balance;
            var beforeFrozen = member.FrozenBalance;

            if (before < amt)
            {
                throw new InvalidOperationException(
                    $"可用余额不足：当前 {before:0.00} 元，本次需扣减 {amt:0.00} 元");
            }

            var afterBalance = before - amt;

            var ok = await CasUpdateAsync(member.Id, before, beforeFrozen, afterBalance, beforeFrozen, null);
            if (!ok)
            {
                await _db.Entry(member).ReloadAsync();
                continue;
            }

            await AppendLogAsync(member.Id, changeType, amt, before, afterBalance,
                relatedType, relatedId, remark, operatorId, now);
            return afterBalance;
        }

        throw new InvalidOperationException("余额扣减并发冲突，请重试");
    }

    /// <summary>
    /// 【CAS 乐观锁】仅当库内 balance / frozen_balance 仍等于期望值时才更新，
    /// 返回是否更新成功（受影响行数=1）。受影响行数=0 表示被并发修改，调用方重读重试。
    /// </summary>
    private async Task<bool> CasUpdateAsync(
        long memberId, decimal expectBalance, decimal expectFrozen,
        decimal newBalance, decimal newFrozen, decimal? newTotalRecharge)
    {
        if (newTotalRecharge.HasValue)
        {
            var total = newTotalRecharge.Value;
            var affected = await _db.MemMembers
                .Where(m => m.Id == memberId && m.Balance == expectBalance && m.FrozenBalance == expectFrozen)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Balance, newBalance)
                    .SetProperty(p => p.FrozenBalance, newFrozen)
                    .SetProperty(p => p.TotalRecharge, total));
            return affected == 1;
        }

        var rows = await _db.MemMembers
            .Where(m => m.Id == memberId && m.Balance == expectBalance && m.FrozenBalance == expectFrozen)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Balance, newBalance)
                .SetProperty(p => p.FrozenBalance, newFrozen));
        return rows == 1;
    }

    /// <summary>写入余额流水（不落库，随调用方事务提交）</summary>
    private async Task AppendLogAsync(
        long memberId, BalanceChangeType changeType, decimal amount,
        decimal beforeBalance, decimal afterBalance,
        int relatedType, long? relatedId, string? remark, long? operatorId, DateTime now)
    {
        await _logRepo.AddAsync(new MktBalanceLog
        {
            MemMemberId = memberId,
            ChangeType = (int)changeType,
            ChangeAmount = amount,
            BeforeBalance = beforeBalance,
            AfterBalance = afterBalance,
            RelatedType = relatedType,
            RelatedId = relatedId,
            Remark = remark,
            OperatorId = operatorId
        });
    }

    /// <summary>取会员实体（必须存在且未被禁用；不存在抛业务异常）</summary>
    private async Task<MemMember> RequireMemberAsync(long memberId)
    {
        var member = await _db.MemMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.IsDeleted == 0);
        if (member == null)
        {
            throw new InvalidOperationException("会员不存在");
        }
        return member;
    }

    /// <summary>只读当前可用余额（幂等返回时用）</summary>
    private async Task<decimal> GetAvailableBalanceAsync(long memberId)
    {
        return await _db.MemMembers.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => m.Balance)
            .FirstOrDefaultAsync();
    }

    /// <summary>充值实体 → DTO（批量补全客户名/编号/状态文案，避免 N+1）</summary>
    private async Task<List<RechargeDto>> MapRechargeDtosAsync(List<MemRecharge> items, bool withBalance)
    {
        if (items.Count == 0)
        {
            return new List<RechargeDto>();
        }

        var memberIds = items.Select(r => r.MemMemberId).Distinct().ToList();
        var memberMap = await _db.MemMembers.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var list = new List<RechargeDto>();
        foreach (var r in items)
        {
            memberMap.TryGetValue(r.MemMemberId, out var m);
            list.Add(new RechargeDto
            {
                Id = r.Id,
                RechargeNo = r.RechargeNo,
                MemMemberId = r.MemMemberId,
                MemMemberName = m?.Name,
                MemberNo = m?.MemberNo,
                RechargeAmount = r.RechargeAmount,
                GiftAmount = r.GiftAmount,
                CreditAmount = r.RechargeAmount + r.GiftAmount,
                // 数据库存的是 pay_type 数值，对外仍暴露渠道码字符串（前端契约不变）
                PayChannel = PayTypeHelper.GetCode(r.PayType),
                // 充值渠道：1-自助 2-后台代充（便于列表区分谁发起）
                RechargeChannel = r.RechargeChannel,
                TransactionId = r.TransactionId,
                Status = r.Status,
                StatusText = RechargeStatusHelper.GetName(r.Status),
                PayTime = r.PayTime,
                CreateTime = r.CreateTime,
                UpdateTime = r.UpdateTime,
                CurrentBalance = withBalance ? (m?.Balance ?? 0m) : null
            });
        }

        return list;
    }

    /// <summary>批量查 sys_user 账号（流水操作人展示）</summary>
    private async Task<Dictionary<long, string>> GetUserNameMapAsync(List<long> userIds)
    {
        var ids = userIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _db.SysUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName);
    }
}
