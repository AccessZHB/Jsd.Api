using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockWarning;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 库存预警服务实现。
///
/// 核心约定（三大业务逻辑）：
/// 1. 【预警检测逻辑】配置表不存预警状态，每次查询实时计算：
///        IsWarning = (Status == 1 启用) 且 (CurrentStock &lt;= WarningStock)
///    停用的配置一律不参与判定（IsWarning 恒 false）；
///    CurrentStock 取法：sku_id 有值 = 该 SKU 库存；sku_id 为空 = 该商品全部 SKU 库存之和。
/// 2. 【参数校验逻辑】商品必须存在；SKU 若传入必须存在且属于该商品；
///    预警阈值不能为负；同一商品 + 同一 SKU（或同一商品的"商品级"配置）只能有一条。
/// 3. 【删除逻辑】本表是纯配置表、不产生业务流水（不像入库/出库/盘点会动库存），
///    因此直接物理删除即可，无需事务与库存回滚；删除后该商品/SKU 不再参与预警。
///
/// 注意：启用/停用是配置开关（status），与"是否触发预警"（IsWarning）是两个概念——
///       停用后即使库存低于阈值也不再预警。
/// </summary>
public class StockWarningService : IStockWarningService
{
    /// <summary>启用状态</summary>
    private const int StatusEnabled = 1;

    /// <summary>停用状态</summary>
    private const int StatusDisabled = 0;

    private readonly AppDbContext _db;
    private readonly IStockWarningRepository _warningRepository;

    public StockWarningService(AppDbContext db, IStockWarningRepository warningRepository)
    {
        _db = db;
        _warningRepository = warningRepository;
    }

    // ============================================================
    // 查询（预警检测：实时算出当前库存与是否触发）
    // ============================================================

    /// <summary>
    /// 分页查询预警配置：多条件筛选 + 关联商品/SKU名称 + 当前库存 + 是否已触发预警
    /// </summary>
    public async Task<ApiResponse<PagedResult<StockWarningItemDto>>> GetPagedListAsync(
        long? prodInfoId, int? status, bool? onlyWarning, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (rows, total) = await _warningRepository.GetPagedListAsync(
            prodInfoId, status, onlyWarning, page, pageSize);

        // 批量查商品与 SKU，回填展示字段（避免 N+1）
        var prodIds = rows.Select(r => r.Warning.ProdInfoId).Distinct().ToList();
        var skuIds = rows
            .Select(r => r.Warning.SkuId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var productMap = prodIds.Count == 0
            ? new Dictionary<long, ProdInfo>()
            : await _db.ProdInfos.AsNoTracking()
                .Where(p => prodIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

        var skuMap = skuIds.Count == 0
            ? new Dictionary<long, ProdSku>()
            : await _db.ProdSkus.AsNoTracking()
                .Where(s => skuIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

        var list = rows.Select(r =>
        {
            var w = r.Warning;
            var dto = new StockWarningItemDto
            {
                Id = w.Id,
                ProdInfoId = w.ProdInfoId,
                SkuId = w.SkuId,
                WarningStock = w.WarningStock,
                Status = w.Status,
                CreateTime = w.CreateTime,
                UpdateTime = w.UpdateTime,
                CurrentStock = r.CurrentStock,
                // 预警检测：启用 且 当前库存 <= 预警阈值
                IsWarning = IsTriggered(w.Status, r.CurrentStock, w.WarningStock)
            };

            if (productMap.TryGetValue(w.ProdInfoId, out var prod))
            {
                dto.ProdInfoName = prod.ProdInfoName;
                dto.ProdInfoCode = prod.ProdInfoCode;
            }

            if (w.SkuId.HasValue && skuMap.TryGetValue(w.SkuId.Value, out var sku))
            {
                dto.SkuCode = sku.SkuCode;
                dto.SkuName = sku.SkuName;
                dto.SpecValues = sku.SpecValues;
            }

            return dto;
        }).ToList();

        return ApiResponse<PagedResult<StockWarningItemDto>>.Success(
            PagedResult<StockWarningItemDto>.Create(list, total, page, pageSize));
    }

    /// <summary>查询单条预警配置（编辑回显）</summary>
    public async Task<ApiResponse<StockWarningItemDto>> GetDetailAsync(long id)
    {
        var entity = await _warningRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse<StockWarningItemDto>.Fail("预警配置不存在", 404);
        }

        var currentStock = await _warningRepository.GetCurrentStockAsync(entity.ProdInfoId, entity.SkuId);

        var dto = new StockWarningItemDto
        {
            Id = entity.Id,
            ProdInfoId = entity.ProdInfoId,
            SkuId = entity.SkuId,
            WarningStock = entity.WarningStock,
            Status = entity.Status,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime,
            CurrentStock = currentStock,
            IsWarning = IsTriggered(entity.Status, currentStock, entity.WarningStock)
        };

        var prod = await _db.ProdInfos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entity.ProdInfoId);
        if (prod != null)
        {
            dto.ProdInfoName = prod.ProdInfoName;
            dto.ProdInfoCode = prod.ProdInfoCode;
        }

        if (entity.SkuId.HasValue)
        {
            var sku = await _db.ProdSkus.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entity.SkuId.Value);
            if (sku != null)
            {
                dto.SkuCode = sku.SkuCode;
                dto.SkuName = sku.SkuName;
                dto.SpecValues = sku.SpecValues;
            }
        }

        return ApiResponse<StockWarningItemDto>.Success(dto);
    }

    // ============================================================
    // 保存（参数校验）
    // ============================================================

    /// <summary>
    /// 新增 / 编辑预警配置。Id 为空或 0 = 新增，否则为编辑。
    /// 业务校验不通过时抛 InvalidOperationException（中文消息），由 Controller 统一转 400。
    /// </summary>
    public async Task<ApiResponse<object>> SaveAsync(StockWarningSaveDto dto)
    {
        var isUpdate = dto.Id.HasValue && dto.Id.Value > 0;

        // --- 编辑时先取实体（跟踪状态，后续直接改字段即可） ---
        LogStockWarning? entity = null;
        if (isUpdate)
        {
            entity = await _warningRepository.GetByIdAsync(dto.Id!.Value);
            if (entity == null)
            {
                return ApiResponse<object>.Fail("预警配置不存在", 404);
            }
        }

        // 【参数校验 1】商品必须存在
        var productExists = await _db.ProdInfos.AsNoTracking().AnyAsync(p => p.Id == dto.ProdInfoId);
        if (!productExists)
        {
            throw new InvalidOperationException("商品不存在");
        }

        // 【参数校验 2】SKU 若传入：必须存在，且必须属于该商品
        if (dto.SkuId.HasValue)
        {
            var sku = await _db.ProdSkus.AsNoTracking()
                .Where(s => s.Id == dto.SkuId.Value)
                .Select(s => new { s.Id, s.ProdInfoId })
                .FirstOrDefaultAsync();

            if (sku == null)
            {
                throw new InvalidOperationException($"SKU不存在（sku_id={dto.SkuId}）");
            }
            if (sku.ProdInfoId != dto.ProdInfoId)
            {
                throw new InvalidOperationException(
                    $"SKU与商品不匹配（sku_id={dto.SkuId} 不属于所选商品）");
            }
        }

        // 【参数校验 3】预警阈值不能为负（DataAnnotations 已校验，此处兜底防止绕过）
        if (dto.WarningStock < 0)
        {
            throw new InvalidOperationException("预警阈值不能为负数");
        }

        // 【参数校验 4】同一商品 + 同一 SKU 只能有一条配置（编辑时排除自身）
        var conflict = await _warningRepository.ExistsConflictAsync(
            dto.ProdInfoId, dto.SkuId, isUpdate ? dto.Id : null);
        if (conflict)
        {
            throw new InvalidOperationException(dto.SkuId.HasValue
                ? "该SKU已配置过预警规则，请勿重复添加"
                : "该商品已配置过商品级预警规则，请勿重复添加");
        }

        // --- 写入 ---
        if (isUpdate && entity != null)
        {
            entity.ProdInfoId = dto.ProdInfoId;
            entity.SkuId = dto.SkuId;
            entity.WarningStock = dto.WarningStock;
            entity.Status = dto.Status;
        }
        else
        {
            entity = new LogStockWarning
            {
                ProdInfoId = dto.ProdInfoId,
                SkuId = dto.SkuId,
                WarningStock = dto.WarningStock,
                Status = dto.Status
            };
            await _warningRepository.AddAsync(entity);
        }

        await _warningRepository.SaveChangesAsync();

        // 回算当前库存与预警状态，便于前端保存后立即提示
        var currentStock = await _warningRepository.GetCurrentStockAsync(entity.ProdInfoId, entity.SkuId);
        var isWarning = IsTriggered(entity.Status, currentStock, entity.WarningStock);

        return ApiResponse<object>.Success(
            new { id = entity.Id, currentStock, isWarning },
            isUpdate ? "预警配置已更新" : "预警配置已新增");
    }

    // ============================================================
    // 启用 / 停用切换
    // ============================================================

    /// <summary>
    /// 启用 / 停用切换：Status 传值则设为指定状态，不传则取反。
    /// 停用后该配置不再参与预警判定（即使库存已低于阈值）。
    /// </summary>
    public async Task<ApiResponse<object>> SetEnableAsync(long id, StockWarningEnableDto dto)
    {
        var entity = await _warningRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse<object>.Fail("预警配置不存在", 404);
        }

        // 不传 Status → 取反切换
        var target = dto.Status ?? (entity.Status == StatusEnabled ? StatusDisabled : StatusEnabled);

        if (target != StatusEnabled && target != StatusDisabled)
        {
            return ApiResponse<object>.Fail("状态值非法（0-停用 1-启用）");
        }

        entity.Status = target;
        await _warningRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(
            new { id = id, status = target },
            target == StatusEnabled ? "已启用" : "已停用");
    }

    // ============================================================
    // 删除
    // ============================================================

    /// <summary>
    /// 删除预警配置。
    /// 【删除逻辑】本表是纯配置表，删除不会牵动库存、不产生/撤销任何业务流水
    /// （区别于入库/出库/盘点单——那些删单要回滚库存），因此直接物理删除，
    /// 无需开启事务。删除后该商品/SKU 不再参与预警检测。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var entity = await _warningRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse<object>.Fail("预警配置不存在", 404);
        }

        _warningRepository.Remove(entity);
        await _warningRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id }, "删除成功");
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    /// <summary>
    /// 【预警检测逻辑】是否触发预警。
    /// 只有启用中的配置才参与判定：启用 且 当前库存 &lt;= 预警阈值 即触发。
    /// </summary>
    private static bool IsTriggered(int status, int currentStock, int warningStock)
        => status == StatusEnabled && currentStock <= warningStock;
}
