using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 退货管理服务（售后模块）
///
/// 【状态机 trx_return.status】
///   0-待审核 / 1-待退货(已审核) ──仓库收货──&gt; 3-仓库已收货
///   3 ──(退货退款)自动触发退款──&gt; 4-已退款
///   任意前置 ──全部拒收──&gt; 5-已拒绝
///
/// 【库存回滚】仓库收货后，按"实收数量"对对应 SKU 做入库(+库存)回滚，并写 log_stock_log。
///
/// 【联动退款】退货单关联"退货退款"类型的退款单时，收货完成且退款仍为待退款状态，
///            自动调用 RefundService.ExecuteRefundCore 执行退款，与收货同事务提交。
///
/// 【幂等】收货按 status 判重：已收货/已退款/已拒绝 不可重复收货。
///
/// 【留痕】每次状态流转写 trx_refund_log。
/// </summary>
public class ReturnService : IReturnService
{
    private const int MaxCasRetry = 5;
    /// <summary>变动类型：入库（对应 log_stock_log.change_type=1）</summary>
    private const int ChangeTypeStockIn = 1;
    /// <summary>关联类型：退货（log_stock_log.ref_type='return'）</summary>
    private const string RefTypeReturn = "return";

    private readonly AppDbContext _db;
    private readonly IReturnRepository _returnRepo;
    private readonly IRepository<TrxReturnItem> _returnItemRepo;
    private readonly IRepository<TrxRefundLog> _logRepo;
    private readonly IRepository<TrxOrder> _orderRepo;
    private readonly IRepository<TrxRefund> _refundRepo;
    private readonly IRepository<ProdSku> _skuRepo;
    private readonly IRepository<MemMember> _memberRepo;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly IRefundService _refundService;
    private readonly CreateReturnValidator _createValidator;
    private readonly ReceiveReturnValidator _receiveValidator;

    public ReturnService(
        AppDbContext db,
        IReturnRepository returnRepo,
        IRepository<TrxReturnItem> returnItemRepo,
        IRepository<TrxRefundLog> logRepo,
        IRepository<TrxOrder> orderRepo,
        IRepository<TrxRefund> refundRepo,
        IRepository<ProdSku> skuRepo,
        IRepository<MemMember> memberRepo,
        IMapper mapper,
        CurrentUserService currentUser,
        IRefundService refundService,
        CreateReturnValidator createValidator,
        ReceiveReturnValidator receiveValidator)
    {
        _db = db;
        _returnRepo = returnRepo;
        _returnItemRepo = returnItemRepo;
        _logRepo = logRepo;
        _orderRepo = orderRepo;
        _refundRepo = refundRepo;
        _skuRepo = skuRepo;
        _memberRepo = memberRepo;
        _mapper = mapper;
        _currentUser = currentUser;
        _refundService = refundService;
        _createValidator = createValidator;
        _receiveValidator = receiveValidator;
    }

    // ============================================================
    // 6. 创建退货单
    // ============================================================
    public async Task<ApiResponse<ReturnCreateResult>> CreateAsync(CreateReturnDto dto)
    {
        _createValidator.ValidateAndThrow(dto);

        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.IsDeleted == 0);
        if (order == null)
        {
            return ApiResponse<ReturnCreateResult>.Fail("订单不存在");
        }

        // 若关联退款单，校验其存在
        if (dto.RefundId.HasValue)
        {
            var linked = await _refundRepo.FirstOrDefaultAsync(r => r.Id == dto.RefundId.Value);
            if (linked == null)
            {
                return ApiResponse<ReturnCreateResult>.Fail("关联的退款单不存在");
            }
        }

        TrxReturn? ret = null;
        for (var i = 0; i < 5; i++)
        {
            var candidate = _returnRepo.GenerateReturnNo();
            if (await _returnRepo.ExistsReturnNoAsync(candidate))
            {
                continue;
            }

            ret = new TrxReturn
            {
                ReturnNo = candidate,
                OrderId = order.Id,
                RefundId = dto.RefundId,
                MemberId = dto.MemberId,
                ReturnReason = dto.ReturnReason,
                ReturnAmount = dto.Items.Sum(x => x.Quantity * x.UnitPrice),
                Status = (int)ReturnStatus.Approved,
                LogisticsCompany = dto.LogisticsCompany,
                LogisticsNo = dto.LogisticsNo,
                ReceiveAddress = dto.ReceiveAddress,
                AdminRemark = dto.AdminRemark
            };

            foreach (var it in dto.Items)
            {
                ret.Items.Add(new TrxReturnItem
                {
                    OrderItemId = it.OrderItemId,
                    ProdInfoId = it.ProdInfoId,
                    SkuId = it.SkuId,
                    ProdInfoName = it.ProdInfoName,
                    SkuName = it.SkuName,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPrice,
                    Subtotal = it.Quantity * it.UnitPrice,
                    ReceivedQuantity = 0,
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

        if (ret == null)
        {
            return ApiResponse<ReturnCreateResult>.Fail("生成退货单号失败，请稍后重试");
        }

        await AppendLogAsync(ret.Id, RefundLogActions.ReturnCreated, _currentUser.UserId,
            _currentUser.UserName, "手动创建退货单");
        await _db.SaveChangesAsync();

        return ApiResponse<ReturnCreateResult>.Success(
            new ReturnCreateResult { Id = ret.Id, ReturnNo = ret.ReturnNo }, "退货单已创建");
    }

    // ============================================================
    // 7. 退货列表分页
    // ============================================================
    public async Task<ApiResponse<PagedResult<ReturnListDto>>> GetPagedListAsync(ReturnQueryDto q)
    {
        var (items, total) = await _returnRepo.GetPagedListAsync(q);

        var ids = items.Select(r => r.Id).Distinct().ToList();
        var orderIds = items.Select(r => r.OrderId).Distinct().ToList();
        var memberIds = items.Select(r => r.MemberId).Distinct().ToList();
        var refundIds = items.Where(r => r.RefundId.HasValue).Select(r => r.RefundId!.Value).Distinct().ToList();

        var allItems = await _returnItemRepo.GetListAsync(i => ids.Contains(i.ReturnId));
        var grouped = allItems.GroupBy(i => i.ReturnId).ToDictionary(
            g => g.Key,
            g => new
            {
                Count = g.Count(),
                TotalQuantity = g.Sum(i => i.Quantity),
                TotalReceived = g.Sum(i => i.ReceivedQuantity)
            });
        var memberNameMap = await _memberRepo.GetListAsync(m => memberIds.Contains(m.Id))
            .ContinueWith(t => t.Result.ToDictionary(m => m.Id, m => m.Name));
        var orderNoMap = await _orderRepo.GetListAsync(o => orderIds.Contains(o.Id))
            .ContinueWith(t => t.Result.ToDictionary(o => o.Id, o => o.OrderNo));
        var refundNoMap = refundIds.Count == 0 ? new Dictionary<long, string>()
            : await _refundRepo.GetListAsync(r => refundIds.Contains(r.Id))
                .ContinueWith(t => t.Result.ToDictionary(r => r.Id, r => r.RefundNo));

        var dtos = items.Select(r =>
        {
            var d = _mapper.Map<ReturnListDto>(r);
            d.MemberName = memberNameMap.TryGetValue(r.MemberId, out var n) ? n : string.Empty;
            d.OrderNo = orderNoMap.TryGetValue(r.OrderId, out var o) ? o : string.Empty;
            d.RefundNo = r.RefundId.HasValue && refundNoMap.TryGetValue(r.RefundId.Value, out var rn) ? rn : string.Empty;
            d.StatusText = ReturnStatusHelper.GetName(r.Status);
            var g = grouped.TryGetValue(r.Id, out var gg) ? gg : null;
            d.ItemCount = g?.Count ?? 0;
            d.TotalQuantity = g?.TotalQuantity ?? 0;
            d.TotalReceived = g?.TotalReceived ?? 0;
            return d;
        }).ToList();

        return ApiResponse<PagedResult<ReturnListDto>>.Success(
            PagedResult<ReturnListDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    // ============================================================
    // 8. 退货详情（含明细）
    // ============================================================
    public async Task<ApiResponse<ReturnDetailDto>> GetDetailAsync(long id)
    {
        var ret = await _returnRepo.GetByIdTrackedAsync(id);
        if (ret == null)
        {
            return ApiResponse<ReturnDetailDto>.Fail("退货单不存在", 404);
        }

        var detail = _mapper.Map<ReturnDetailDto>(ret);
        detail.StatusText = ReturnStatusHelper.GetName(ret.Status);
        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == ret.MemberId);
        detail.MemberName = member?.Name ?? string.Empty;
        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == ret.OrderId);
        detail.OrderNo = order?.OrderNo ?? string.Empty;
        if (ret.RefundId.HasValue)
        {
            var refund = await _refundRepo.FirstOrDefaultAsync(r => r.Id == ret.RefundId.Value);
            detail.RefundNo = refund?.RefundNo ?? string.Empty;
        }

        detail.Items = ret.Items.Select(i => new ReturnDetailItemDto
        {
            Id = i.Id,
            OrderItemId = i.OrderItemId,
            ProdInfoId = i.ProdInfoId,
            SkuId = i.SkuId,
            ProdInfoName = i.ProdInfoName,
            SkuName = i.SkuName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.Subtotal,
            Status = i.Status,
            StatusText = ReturnItemStatusHelper.GetName(i.Status)
        }).ToList();

        return ApiResponse<ReturnDetailDto>.Success(detail);
    }

    // ============================================================
    // 9. 仓库确认收货（库存回滚 + 自动触发退款）
    // ============================================================
    public async Task<ApiResponse<object>> ReceiveAsync(long id, ReceiveReturnDto dto)
    {
        _receiveValidator.ValidateAndThrow(dto);

        var ret = await _returnRepo.GetByIdTrackedAsync(id);
        if (ret == null)
        {
            return ApiResponse<object>.Fail("退货单不存在", 404);
        }

        // 幂等：已收货/已退款/已拒绝 不可重复收货
        if (ret.Status == (int)ReturnStatus.Received
            || ret.Status == (int)ReturnStatus.Refunded
            || ret.Status == (int)ReturnStatus.Rejected)
        {
            return ApiResponse<object>.Fail("该退货单已收货或已处理，不可重复收货");
        }

        // 仅"待退货(1)/已寄回(2)"可收货
        if (ret.Status != (int)ReturnStatus.Approved && ret.Status != (int)ReturnStatus.Shipped)
        {
            return ApiResponse<object>.Fail("仅待退货/已寄回的退货单可收货");
        }

        // 校验实收明细：行必须属于本退货单，且实收 ≤ 申请
        var itemIdSet = ret.Items.Select(i => i.Id).ToHashSet();
        foreach (var line in dto.Items)
        {
            if (!itemIdSet.Contains(line.ItemId))
            {
                return ApiResponse<object>.Fail($"退货明细行 {line.ItemId} 不属于本退货单");
            }
            var item = ret.Items.First(i => i.Id == line.ItemId);
            if (line.ReceivedQuantity > item.Quantity)
            {
                return ApiResponse<object>.Fail(
                    $"商品「{item.ProdInfoName}」实收数量({line.ReceivedQuantity}) 不能超过申请数量({item.Quantity})");
            }
        }

        // 写回实收数量与行状态
        var stockLogs = new List<LogStockLog>();
        var stockCache = new Dictionary<long, int>();
        var anyReceived = false;
        foreach (var line in dto.Items)
        {
            var item = ret.Items.First(i => i.Id == line.ItemId);
            item.ReceivedQuantity = line.ReceivedQuantity;
            if (line.ReceivedQuantity > 0)
            {
                item.Status = (int)ReturnItemStatus.Received;
                anyReceived = true;
                // 仅当存在 SKU 时才回滚库存
                if (item.SkuId.HasValue && item.SkuId.Value > 0)
                {
                    await AddStockAsync(stockLogs, stockCache,
                        item.ProdInfoId, item.SkuId.Value, line.ReceivedQuantity,
                        $"退货回滚 退货单{ret.ReturnNo}", ret.Id);
                }
            }
            else
            {
                item.Status = (int)ReturnItemStatus.Rejected; // 0 实收 = 拒收
            }
        }

        if (anyReceived)
        {
            ret.Status = (int)ReturnStatus.Received;
            ret.HandleTime = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(dto.AdminRemark))
            {
                ret.AdminRemark = dto.AdminRemark;
            }
            await _db.LogStockLogs.AddRangeAsync(stockLogs);
            await AppendLogAsync(ret.Id, RefundLogActions.ReturnReceived, _currentUser.UserId,
                _currentUser.UserName, dto.AdminRemark);

            // 联动退款：关联"退货退款"且仍为待退款的退款单，自动执行
            if (ret.RefundId.HasValue)
            {
                var refund = await _refundRepo.FirstOrDefaultAsync(r => r.Id == ret.RefundId.Value);
                if (refund != null
                    && refund.RefundType == (int)RefundType.ReturnAndRefund
                    && refund.Status == (int)RefundStatus.Approved)
                {
                    await _refundService.ExecuteRefundCoreAsync(refund, _currentUser.UserId, _currentUser.UserName);
                }
            }
        }
        else
        {
            // 全部拒收
            ret.Status = (int)ReturnStatus.Rejected;
            ret.HandleTime = DateTime.Now;
            await AppendLogAsync(ret.Id, RefundLogActions.ReturnReceived, _currentUser.UserId,
                _currentUser.UserName, "仓库收货-全部拒收（未收回任何商品）");
        }

        await _db.SaveChangesAsync();
        return ApiResponse<object>.Success(new { id }, anyReceived ? "收货成功，库存已回滚" : "已全部拒收");
    }

    // ============================================================
    // 10. 根据退款单查询关联退货单
    // ============================================================
    public async Task<ApiResponse<List<ReturnListDto>>> GetByRefundAsync(long refundId)
    {
        var returns = await _returnRepo.GetByRefundIdAsync(refundId);
        if (returns.Count == 0)
        {
            return ApiResponse<List<ReturnListDto>>.Success(new List<ReturnListDto>());
        }

        var ids = returns.Select(r => r.Id).ToList();
        var memberIds = returns.Select(r => r.MemberId).Distinct().ToList();
        var orderIds = returns.Select(r => r.OrderId).Distinct().ToList();
        var itemsByReturn = await _returnItemRepo.GetListAsync(i => ids.Contains(i.ReturnId));
        var grouped = itemsByReturn.GroupBy(i => i.ReturnId).ToDictionary(
            g => g.Key,
            g => new
            {
                Count = g.Count(),
                TotalQuantity = g.Sum(i => i.Quantity),
                TotalReceived = g.Sum(i => i.ReceivedQuantity)
            });
        var memberNameMap = await _memberRepo.GetListAsync(m => memberIds.Contains(m.Id))
            .ContinueWith(t => t.Result.ToDictionary(m => m.Id, m => m.Name));
        var orderNoMap = await _orderRepo.GetListAsync(o => orderIds.Contains(o.Id))
            .ContinueWith(t => t.Result.ToDictionary(o => o.Id, o => o.OrderNo));

        var dtos = returns.Select(r =>
        {
            var d = _mapper.Map<ReturnListDto>(r);
            d.MemberName = memberNameMap.TryGetValue(r.MemberId, out var n) ? n : string.Empty;
            d.OrderNo = orderNoMap.TryGetValue(r.OrderId, out var o) ? o : string.Empty;
            d.StatusText = ReturnStatusHelper.GetName(r.Status);
            var g = grouped.TryGetValue(r.Id, out var gg) ? gg : null;
            d.ItemCount = g?.Count ?? 0;
            d.TotalQuantity = g?.TotalQuantity ?? 0;
            d.TotalReceived = g?.TotalReceived ?? 0;
            return d;
        }).ToList();

        return ApiResponse<List<ReturnListDto>>.Success(dtos);
    }

    /// <summary>
    /// 【乐观锁 CAS】回滚（增加）SKU 库存并写变动日志（与 OrderService.DeductStockAsync 对称）。
    /// </summary>
    private async Task AddStockAsync(
        List<LogStockLog> logs, Dictionary<long, int> stockCache,
        long prodInfoId, long skuId, int delta, string remark, long refId)
    {
        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            var current = stockCache.TryGetValue(skuId, out var cached)
                ? cached
                : await _skuRepo.Query().Where(s => s.Id == skuId)
                    .Select(s => s.Stock).FirstAsync();

            var after = current + delta;

            var affected = await _skuRepo.Query().Where(s => s.Id == skuId && s.Stock == current)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, after));

            if (affected == 1)
            {
                stockCache[skuId] = after;
                logs.Add(new LogStockLog
                {
                    ProdInfoId = prodInfoId,
                    SkuId = skuId,
                    ChangeType = ChangeTypeStockIn,
                    ChangeQty = delta,
                    BeforeStock = current,
                    AfterStock = after,
                    RefType = RefTypeReturn,
                    RefId = refId,
                    Remark = remark,
                    Operator = _currentUser.UserName
                });
                return;
            }
        }

        throw new InvalidOperationException($"SKU(sku_id={skuId}) 库存更新并发冲突，请重试");
    }

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
}
