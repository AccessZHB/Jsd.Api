using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 应付账款（采购付款）服务实现。
///
/// 【付款核销】PayAsync 在单个事务内：
///   1) 校验应付单存在且未结清；
///   2) 校验本次付款金额 ≤ 未付金额；
///   3) paid_amount += 金额、unpaid_amount -= 金额；
///   4) 自动更新状态：unpaid == 0 → 已结清(2)；paid&gt;0 且 unpaid&gt;0 → 部分付款(1)；否则 未付(0)。
///
/// 【注意】付款流水目前未单独落表（规范未定义付款流水表），如需对账可追溯，
///   可在后续"操作日志/付款流水"模块建成后补充 pur_payable_pay_log。
/// </summary>
public class PayableService : IPayableService
{
    private readonly AppDbContext _db;
    private readonly IRepository<SupSupplier> _supplierRepository;
    private readonly IMapper _mapper;
    private readonly PayPaymentValidator _payValidator;

    public PayableService(
        AppDbContext db,
        IRepository<SupSupplier> supplierRepository,
        IMapper mapper,
        PayPaymentValidator payValidator)
    {
        _db = db;
        _supplierRepository = supplierRepository;
        _mapper = mapper;
        _payValidator = payValidator;
    }

    // ============================================================
    // 查询
    // ============================================================

    public async Task<ApiResponse<PagedResult<PayableListDto>>> GetPagedListAsync(PayableQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 || query.PageSize > 100 ? 20 : query.PageSize;

        var q = _db.PurPayables.AsNoTracking();

        if (query.SupplierId.HasValue)
        {
            q = q.Where(p => p.SupplierId == query.SupplierId.Value);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(p => p.Status == query.Status.Value);
        }

        if (query.DueDateStart.HasValue)
        {
            q = q.Where(p => p.DueDate >= query.DueDateStart.Value);
        }

        if (query.DueDateEnd.HasValue)
        {
            q = q.Where(p => p.DueDate <= query.DueDateEnd.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(p => p.PayableNo.Contains(kw)
                              || (p.Supplier != null && p.Supplier.SupplierName.Contains(kw)));
        }

        var total = await q.CountAsync();
        var rows = await q.Include(p => p.Supplier)
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = rows.Select(MapListDto).ToList();
        return ApiResponse<PagedResult<PayableListDto>>.Success(
            PagedResult<PayableListDto>.Create(list, total, page, pageSize));
    }

    public async Task<ApiResponse<PayableDetailDto>> GetDetailAsync(long id)
    {
        var payable = await _db.PurPayables.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payable == null)
        {
            return ApiResponse<PayableDetailDto>.Fail("应付账款不存在", 404);
        }

        var dto = new PayableDetailDto
        {
            Id = payable.Id,
            PayableNo = payable.PayableNo,
            SupplierId = payable.SupplierId,
            SupplierName = payable.Supplier?.SupplierName,
            RelatedOrderId = payable.RelatedOrderId,
            RelatedOrderNo = payable.Order?.OrderNo,
            TotalAmount = payable.TotalAmount,
            PaidAmount = payable.PaidAmount,
            UnpaidAmount = payable.UnpaidAmount,
            DueDate = payable.DueDate,
            Status = payable.Status,
            StatusText = PayableStatusHelper.GetName(payable.Status),
            Remark = payable.Remark,
            CreateTime = payable.CreateTime,
            UpdateTime = payable.UpdateTime
        };

        return ApiResponse<PayableDetailDto>.Success(dto);
    }

    public async Task<ApiResponse<List<PayableListDto>>> GetBySupplierAsync(long supplierId, int? status)
    {
        var q = _db.PurPayables.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.SupplierId == supplierId);

        if (status.HasValue)
        {
            q = q.Where(p => p.Status == status.Value);
        }

        var rows = await q.OrderByDescending(p => p.Id).ToListAsync();
        return ApiResponse<List<PayableListDto>>.Success(rows.Select(MapListDto).ToList());
    }

    // ============================================================
    // 付款核销（核心）
    // ============================================================

    public async Task<ApiResponse<object>> PayAsync(long id, PayPaymentDto dto)
    {
        _payValidator.ValidateAndThrow(dto);

        var payable = await _db.PurPayables.FirstOrDefaultAsync(p => p.Id == id);
        if (payable == null)
        {
            return ApiResponse<object>.Fail("应付账款不存在", 404);
        }

        if (payable.Status == (int)PayableStatus.Settled)
        {
            return ApiResponse<object>.Fail("该应付账款已结清，不可重复付款");
        }

        var amount = decimal.Round(dto.PayAmount, 2, MidpointRounding.AwayFromZero);
        if (amount > payable.UnpaidAmount)
        {
            return ApiResponse<object>.Fail(
                $"付款金额({amount})超过未付金额({payable.UnpaidAmount})");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            payable.PaidAmount = decimal.Round(payable.PaidAmount + amount, 2, MidpointRounding.AwayFromZero);
            payable.UnpaidAmount = decimal.Round(payable.UnpaidAmount - amount, 2, MidpointRounding.AwayFromZero);

            if (payable.UnpaidAmount <= 0)
            {
                payable.UnpaidAmount = 0;
                payable.Status = (int)PayableStatus.Settled;   // 已结清
            }
            else if (payable.PaidAmount > 0)
            {
                payable.Status = (int)PayableStatus.Partial;   // 部分付款
            }
            else
            {
                payable.Status = (int)PayableStatus.Unpaid;    // 未付（理论不会走到，amount>0）
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ApiResponse<object>.Success(new { id }, "付款成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    private static PayableListDto MapListDto(PurPayable p) => new()
    {
        Id = p.Id,
        PayableNo = p.PayableNo,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier?.SupplierName,
        RelatedOrderId = p.RelatedOrderId,
        TotalAmount = p.TotalAmount,
        PaidAmount = p.PaidAmount,
        UnpaidAmount = p.UnpaidAmount,
        DueDate = p.DueDate,
        Status = p.Status,
        StatusText = PayableStatusHelper.GetName(p.Status),
        Remark = p.Remark,
        CreateTime = p.CreateTime
    };
}
