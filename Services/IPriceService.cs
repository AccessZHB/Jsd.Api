using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;

namespace Jsd.Api.Services;

/// <summary>
/// 价格策略服务接口（A组）
///
/// 覆盖：客户等级 CRUD/启停、价格策略 CRUD/提交/停用、规则批量维护、
///       取价引擎（/api/price/quotation）、价格变更日志、订单价格快照。
///
/// 核心原则：价格计算必须可追溯（快照），价格变更必须可审计（price_change_log）。
/// </summary>
public interface IPriceService
{
    // ==================== 一、客户（会员）等级 ====================

    /// <summary>分页查询客户等级</summary>
    Task<ApiResponse<PagedResult<MemberLevelDto>>> GetLevelsAsync(MemberLevelQueryDto q);

    /// <summary>新增客户等级（校验 level_code 唯一）</summary>
    Task<ApiResponse<long>> CreateLevelAsync(CreateMemberLevelDto dto);

    /// <summary>更新客户等级</summary>
    Task<ApiResponse<bool>> UpdateLevelAsync(long id, UpdateMemberLevelDto dto);

    /// <summary>启用/停用客户等级（停用前校验是否有关联的启用中策略）</summary>
    Task<ApiResponse<bool>> UpdateLevelStatusAsync(long id, MemberLevelStatusDto dto);

    // ==================== 二、价格策略 ====================

    /// <summary>分页查询价格策略</summary>
    Task<ApiResponse<PagedResult<PriceStrategyDto>>> GetStrategiesAsync(PriceStrategyQueryDto q);

    /// <summary>新增价格策略（默认草稿，自动生成 strategy_no）</summary>
    Task<ApiResponse<long>> CreateStrategyAsync(CreatePriceStrategyDto dto);

    /// <summary>更新策略基本信息（仅草稿可改）</summary>
    Task<ApiResponse<bool>> UpdateStrategyAsync(long id, UpdatePriceStrategyDto dto);

    /// <summary>草稿提交启用（校验规则完整性）</summary>
    Task<ApiResponse<bool>> SubmitStrategyAsync(long id);

    /// <summary>停用策略（记录变更日志）</summary>
    Task<ApiResponse<bool>> DisableStrategyAsync(long id, string? reason);

    /// <summary>删除策略（仅草稿；连同其规则与明细一并删除，保留变更日志用于审计）</summary>
    Task<ApiResponse<bool>> DeleteStrategyAsync(long id);

    /// <summary>查询策略下的规则及明细</summary>
    Task<ApiResponse<List<PriceRuleDto>>> GetRulesAsync(long strategyId);

    /// <summary>批量维护规则及明细（全量覆盖，事务内执行，记录变更日志）</summary>
    Task<ApiResponse<bool>> SaveRulesBatchAsync(long strategyId, PriceRuleBatchSaveDto dto);

    /// <summary>
    /// 商品物料（SKU）下拉搜索：供价格规则「指定商品 / 阶梯价」明细行选择商品。
    /// 按 SKU 名称、SKU 编码、商品名称模糊匹配，返回零售价（标准售价）便于配置时对照。
    /// </summary>
    Task<ApiResponse<List<MaterialOptionDto>>> GetMaterialsAsync(string? keyword, int limit = 50);

    // ==================== 三、取价引擎 ====================

    /// <summary>
    /// 核心取价：按优先级 客户专属价 &gt; 促销价 &gt; 客户等级价 &gt; 批量阶梯价 &gt; 商品标准售价 匹配，
    /// 输出每个商品的最终价、命中策略、折扣信息、是否触发底价保护。
    /// </summary>
    Task<ApiResponse<List<QuotationResultDto>>> QuotationAsync(QuotationRequestDto dto);

    // ==================== 四、订单价格快照 ====================

    /// <summary>内部接口：下单时批量写入价格快照</summary>
    Task<ApiResponse<bool>> SaveSnapshotsAsync(OrderPriceSnapshotBatchDto dto);

    /// <summary>分页查询订单价格快照（订单号 / 客户 / 商品 / 命中策略类型 / 时间范围）</summary>
    Task<ApiResponse<PagedResult<OrderPriceSnapshotDto>>> GetSnapshotsPagedAsync(OrderPriceSnapshotQueryDto q);

    /// <summary>按订单查询价格快照（历史价格追溯）</summary>
    Task<ApiResponse<List<OrderPriceSnapshotDto>>> GetSnapshotsAsync(long orderId);

    // ==================== 五、价格变更日志 ====================

    /// <summary>分页查询价格变更日志</summary>
    Task<ApiResponse<PagedResult<PriceChangeLogDto>>> GetChangeLogsAsync(PriceChangeLogQueryDto q);
}
