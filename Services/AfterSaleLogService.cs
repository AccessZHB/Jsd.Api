using Jsd.Api.Entities;
using Jsd.Api.Models.AfterSale;
using Jsd.Api.Models.Common;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 售后日志服务（trx_refund_log 只读查询）
/// 所有售后写操作都已由 RefundService / ReturnService 在业务事务内写入日志，
/// 本服务仅提供"按退款单 / 按退货单"的轨迹查询。
/// </summary>
public class AfterSaleLogService : IAfterSaleLogService
{
    private readonly IRepository<TrxRefundLog> _logRepo;
    private readonly IRepository<TrxReturn> _returnRepo;

    public AfterSaleLogService(IRepository<TrxRefundLog> logRepo, IRepository<TrxReturn> returnRepo)
    {
        _logRepo = logRepo;
        _returnRepo = returnRepo;
    }

    public async Task<ApiResponse<List<RefundLogDto>>> GetByRefundAsync(long refundId)
    {
        var logs = await _logRepo.GetListAsync(l => l.RefundId == refundId);
        var dtos = logs.OrderBy(l => l.CreateTime).Select(l => new RefundLogDto
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

        return ApiResponse<List<RefundLogDto>>.Success(dtos);
    }

    public async Task<ApiResponse<List<RefundLogDto>>> GetByReturnAsync(long returnId)
    {
        var ret = await _returnRepo.FirstOrDefaultAsync(r => r.Id == returnId);
        if (ret == null)
        {
            return ApiResponse<List<RefundLogDto>>.Fail("退货单不存在", 404);
        }

        // 退货单日志通过"关联退款单"归集（trx_refund_log.refund_id）
        if (!ret.RefundId.HasValue)
        {
            return ApiResponse<List<RefundLogDto>>.Success(new List<RefundLogDto>());
        }

        return await GetByRefundAsync(ret.RefundId.Value);
    }
}
