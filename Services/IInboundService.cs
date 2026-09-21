using Jsd.Api.Models.Common;
using Jsd.Api.Models.Purchase;

namespace Jsd.Api.Services;

/// <summary>
/// 采购入库（以采定入）服务接口。
/// 核心约定：
///   - 入库单只能基于"审批通过/待入库"(status=2) 的采购订单创建；
///   - 创建即生成"待确认"(0) 的入库单，实收数量可 ≤ 订单数量（分批到货），
///     并做超量拦截：已确认入库总数 + 本次 ≤ 订单数量；
///   - 确认(Confirm) 时才真正增加库存、写变动日志、自动生成应付账款、推进采购订单状态。
/// </summary>
public interface IInboundService
{
    /// <summary>分页查询入库单列表</summary>
    Task<ApiResponse<PagedResult<InboundListDto>>> GetPagedListAsync(InboundQueryDto query);

    /// <summary>入库单详情（主表 + 明细，明细带物料名/采购单价）</summary>
    Task<ApiResponse<InboundDetailDto>> GetDetailAsync(long id);

    /// <summary>按采购订单查询其下所有入库单</summary>
    Task<ApiResponse<List<InboundListDto>>> GetByOrderAsync(long orderId);

    /// <summary>创建入库单（以采定入核心入口，生成待确认单）</summary>
    Task<ApiResponse<object>> CreateAsync(CreateInboundDto dto);

    /// <summary>确认入库：增库存 + 写日志 + 生成应付 + 推进订单状态</summary>
    Task<ApiResponse<object>> ConfirmAsync(long id);
}
