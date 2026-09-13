using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.StockCheck;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 库存盘点服务实现。
/// 核心约定：
/// 1. 创建/完成/删除全部使用 IDbContextTransaction 事务，主表、明细、库存、变动日志要么全成功要么全回滚；
/// 2. 创建时把每个 SKU 的当前 prod_sku.stock 作为 system_stock 快照写入明细，
///    状态置 盘点中(0)，并写一条"快照"变动日志（change_qty=0，审计用）；
/// 3. 完成盘点时录入实盘 actual_stock，diff_qty = actual - system，按差值用【乐观锁 CAS】
///    调整 prod_sku.stock，并写"盘点调整"变动日志，状态置 已完成(1)；
/// 4. 删除仅允许 盘点中(0) 单据，且不调整库存（未实盘、未改动库存）；
/// 5. 库存调整采用乐观锁 CAS（prod_sku 无并发令牌列，不改表结构）：
///    UPDATE prod_sku SET stock=@after WHERE id=@id AND stock=@current，受影响行=0 重试（最多3次）；
/// 6. 变动日志 log_stock_log.change_type=3（盘点调整），ref_type='check'；
/// 7. 本系统无审核环节，盘点单"已完成"即终态。
/// </summary>
public class StockCheckService : IStockCheckService
{
    /// <summary>变动类型：盘点调整（对应 log_stock_log.change_type=3）</summary>
    private const int ChangeTypeCheck = 3;

    /// <summary>关联类型：盘点（log_stock_log.ref_type='check'）</summary>
    private const string RefTypeCheck = "check";

    /// <summary>盘点单号前缀（PD = PanDian）</summary>
    private const string NoPrefix = "PD";

    /// <summary>乐观锁 CAS 最大重试次数</summary>
    private const int MaxCasRetry = 3;

    private readonly AppDbContext _db;
    private readonly ILogStockCheckRepository _checkRepository;
    private readonly ILogStockCheckItemRepository _itemRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IRepository<ProdSku> _skuRepository;

    public StockCheckService(
        AppDbContext db,
        ILogStockCheckRepository checkRepository,
        ILogStockCheckItemRepository itemRepository,
        IRepository<ProdInfo> productRepository,
        IRepository<ProdSku> skuRepository)
    {
        _db = db;
        _checkRepository = checkRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    // ============================================================
    // 查询
    // ============================================================

    /// <summary>
    /// 分页查询盘点单列表（多条件筛选 + 差异汇总）
    /// </summary>
    public async Task<ApiResponse<PagedResult<StockCheckListDto>>> GetPagedListAsync(
        string? keyword, int? status, DateTime? startTime, DateTime? endTime, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, total) = await _checkRepository.GetPagedListAsync(
            keyword, status, startTime, endTime, page, pageSize);

        var list = orders.Select(MapToListDto).ToList();

        // 一次性统计当前页每张单的明细汇总（系统/实盘/盈亏合计 + 明细数），避免 N+1
        var orderIds = list.Select(o => o.Id).ToList();
        if (orderIds.Count > 0)
        {
            var summaries = await _db.LogStockCheckItems
                .Where(i => orderIds.Contains(i.CheckId))
                .GroupBy(i => i.CheckId)
                .Select(g => new
                {
                    CheckId = g.Key,
                    ItemCount = g.Count(),
                    TotalSystem = g.Sum(x => x.SystemStock),
                    TotalActual = g.Sum(x => x.ActualStock),
                    TotalDiff = g.Sum(x => x.DiffQty)
                })
                .ToDictionaryAsync(x => x.CheckId, x => x);

            foreach (var dto in list)
            {
                if (summaries.TryGetValue(dto.Id, out var s))
                {
                    dto.ItemCount = s.ItemCount;
                    dto.TotalSystemStock = s.TotalSystem;
                    dto.TotalActualStock = s.TotalActual;
                    dto.TotalDiffQty = s.TotalDiff;
                }
            }
        }

        return ApiResponse<PagedResult<StockCheckListDto>>.Success(
            PagedResult<StockCheckListDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 获取盘点单详情（主表 + 明细，明细带商品名称、SKU 编码/名称）
    /// </summary>
    public async Task<ApiResponse<StockCheckDetailDto>> GetDetailAsync(long id)
    {
        var order = await _checkRepository.GetDetailAsync(id);
        if (order == null)
        {
            return ApiResponse<StockCheckDetailDto>.Fail("盘点单不存在", 404);
        }

        var dto = new StockCheckDetailDto
        {
            Id = order.Id,
            CheckNo = order.CheckNo,
            Status = order.Status,
            Remark = order.Remark,
            Operator = order.Operator,
            CreateTime = order.CreateTime,
            UpdateTime = order.UpdateTime,
            ItemCount = order.Items.Count,
            TotalSystemStock = order.Items.Sum(i => i.SystemStock),
            TotalActualStock = order.Items.Sum(i => i.ActualStock),
            TotalDiffQty = order.Items.Sum(i => i.DiffQty)
        };

        // 批量查商品与 SKU，回填展示字段（避免 N+1）
        var prodIds = order.Items.Select(i => i.ProdInfoId).Distinct().ToList();
        var skuIds = order.Items.Where(i => i.SkuId.HasValue).Select(i => i.SkuId!.Value).Distinct().ToList();

        var productMap = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ProdInfoName);

        var skuMap = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        dto.Items = order.Items.Select(i =>
        {
            var itemDto = MapToItemDto(i);
            itemDto.ProdInfoName = productMap.TryGetValue(i.ProdInfoId, out var pn) ? pn : null;
            if (i.SkuId.HasValue && skuMap.TryGetValue(i.SkuId.Value, out var sku))
            {
                itemDto.SkuCode = sku.SkuCode;
                itemDto.SkuName = sku.SkuName;
            }
            return itemDto;
        }).ToList();

        return ApiResponse<StockCheckDetailDto>.Success(dto);
    }

    // ============================================================
    // 创建（带入系统库存 + 写快照日志）
    // ============================================================

    /// <summary>
    /// 创建盘点单：生成单号 → 批量读取各 SKU 当前库存作为 system_stock 快照 → 写主表/明细 → 写变动日志，
    /// 全程事务，状态置 盘点中(0)。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(StockCheckCreateDto dto)
    {
        // 1. 事务前先做关联校验（商品/SKU 不存在、归属错误抛业务异常，由 Controller 统一转 400）
        await ValidateRelationsAsync(dto.Items);

        // 2. 生成不重复的盘点单号
        var checkNo = await GenerateCheckNoAsync();

        // 3. 批量读取各 SKU 当前系统库存（用于快照）
        var skuIds = dto.Items.Select(i => i.SkuId!.Value).Distinct().ToList();
        var stockMap = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Stock);

        // 4. 开启事务
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 4.1 写主表（状态=盘点中）
            var order = new LogStockCheck
            {
                CheckNo = checkNo,
                Status = 0,
                Remark = dto.Remark,
                Operator = dto.Operator
            };
            await _checkRepository.AddAsync(order);
            await _checkRepository.SaveChangesAsync();

            // 4.2 写明细（系统库存快照，实盘/盈亏初始为 0）+ 写"创建快照"变动日志
            var itemList = new List<LogStockCheckItem>();
            var logList = new List<LogStockLog>();
            foreach (var input in dto.Items)
            {
                var systemStock = stockMap[input.SkuId!.Value];
                itemList.Add(new LogStockCheckItem
                {
                    CheckId = order.Id,
                    ProdInfoId = input.ProdInfoId,
                    SkuId = input.SkuId,
                    SystemStock = systemStock,
                    ActualStock = 0,
                    DiffQty = 0,
                    Remark = input.Remark
                });

                // 创建快照日志：change_qty=0，before/after 均为系统库存（盘点基线审计）
                logList.Add(new LogStockLog
                {
                    ProdInfoId = input.ProdInfoId,
                    SkuId = input.SkuId,
                    ChangeType = ChangeTypeCheck,
                    ChangeQty = 0,
                    BeforeStock = systemStock,
                    AfterStock = systemStock,
                    RefType = RefTypeCheck,
                    RefId = order.Id,
                    Remark = "创建盘点单-快照系统库存",
                    Operator = dto.Operator
                });
            }

            await _itemRepository.AddRangeAsync(itemList);
            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = order.Id, checkNo = order.CheckNo }, "盘点单创建成功（盘点中）");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 完成盘点（录入实盘 + 计算盈亏 + 调整库存 + 写调整日志）
    // ============================================================

    /// <summary>
    /// 完成盘点：仅 盘点中(0) 单据可调用。逐 SKU 计算盈亏 = 实盘 - 系统，按差值乐观锁调整库存，
    /// 写盘点调整日志，状态置 已完成(1)，全程事务。
    /// </summary>
    public async Task<ApiResponse<object>> CompleteAsync(long id, StockCheckUpdateDto dto)
    {
        // 1. 主表必须存在且为 盘点中(0)
        var order = await _checkRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("盘点单不存在", 404);
        }
        if (order.Status != 0)
        {
            return ApiResponse<object>.Fail("仅盘点中单据可完成盘点（已完成单据不可重复操作）");
        }

        // 2. 取已创建的明细（系统库存快照在此）
        var existingItems = await _itemRepository.GetByCheckIdAsync(id);
        if (existingItems.Count == 0)
        {
            return ApiResponse<object>.Fail("盘点单无明细，无法完成");
        }

        // 3. 入参 SKU 必须合法且完整覆盖已创建明细
        var existingBySku = existingItems.ToDictionary(i => i.SkuId!.Value, i => i);
        var inputSkuSet = new HashSet<long>(dto.Items.Select(i => i.SkuId));
        foreach (var input in dto.Items)
        {
            if (!existingBySku.TryGetValue(input.SkuId, out _))
            {
                return ApiResponse<object>.Fail($"SKU(sku_id={input.SkuId}) 不在本盘点单明细中");
            }
        }
        foreach (var existing in existingItems)
        {
            if (!inputSkuSet.Contains(existing.SkuId!.Value))
            {
                return ApiResponse<object>.Fail($"缺少 SKU(sku_id={existing.SkuId}) 的实盘数量");
            }
        }

        // 4. 开启事务
        var logList = new List<LogStockLog>();
        var stockCache = new Dictionary<long, int>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var input in dto.Items)
            {
                var item = existingBySku[input.SkuId];
                var diff = input.ActualStock - item.SystemStock; // 盈亏 = 实盘 - 系统

                // 在跟踪实例上直接更新明细字段（SaveChanges 一并提交）
                item.ActualStock = input.ActualStock;
                item.DiffQty = diff;
                item.Remark = input.Remark;

                // 按盈亏差值调整库存（delta = diff，可正可负）；乐观锁 CAS
                await AdjustStockAndLogAsync(
                    logList, stockCache,
                    item.ProdInfoId, input.SkuId, diff,
                    "盘点调整（实盘-系统）", dto.Operator, id);
            }

            // 状态置 已完成；同步备注/操作人
            order.Status = 1;
            order.Remark = dto.Remark;
            order.Operator = dto.Operator;

            await _db.LogStockLogs.AddRangeAsync(logList);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id, checkNo = order.CheckNo }, "盘点完成，库存已调整");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 删除（仅盘点中可删）
    // ============================================================

    /// <summary>
    /// 删除盘点单：仅 盘点中(0) 可删（已完成单据已调整过库存，禁止删除导致账实不符）。
    /// 盘点中单据尚未录入实盘、未改动库存，故删除不影响库存，仅删明细与主表。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var order = await _checkRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<object>.Fail("盘点单不存在", 404);
        }
        if (order.Status != 0)
        {
            return ApiResponse<object>.Fail("仅盘点中单据可删除（已完成单据已调整库存，不可删除）");
        }

        var items = await _itemRepository.GetByCheckIdAsync(id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (items.Count > 0)
            {
                await _itemRepository.RemoveByCheckIdAsync(id);
            }
            _checkRepository.Remove(order);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = id }, "删除成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    /// <summary>
    /// 关联数据校验：商品存在性、SKU 存在性及 SKU 归属。
    /// 不通过时抛出 InvalidOperationException（中文消息），由 Controller 统一捕获返回 400。
    /// </summary>
    private async Task ValidateRelationsAsync(List<StockCheckItemInputDto> items)
    {
        var prodIds = items.Select(i => i.ProdInfoId).Distinct().ToList();
        var existingProdIds = await _db.ProdInfos
            .AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();
        if (prodIds.Any(pid => !existingProdIds.Contains(pid)))
        {
            throw new InvalidOperationException("商品不存在");
        }

        var skuIds = items.Where(i => i.SkuId.HasValue).Select(i => i.SkuId!.Value).Distinct().ToList();
        var skuList = await _db.ProdSkus
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Select(s => new { s.Id, s.ProdInfoId })
            .ToListAsync();

        var skuDict = skuList.ToDictionary(s => s.Id, s => s.ProdInfoId);
        foreach (var item in items)
        {
            var skuId = item.SkuId!.Value;
            if (!skuDict.TryGetValue(skuId, out var skuProdInfoId))
            {
                throw new InvalidOperationException($"SKU不存在（sku_id={skuId}）");
            }

            // 防止前端把别的商品的 SKU 挂到当前明细上
            if (skuProdInfoId != item.ProdInfoId)
            {
                throw new InvalidOperationException(
                    $"SKU与商品不匹配（sku_id={skuId} 不属于 prod_info_id={item.ProdInfoId}）");
            }
        }
    }

    /// <summary>
    /// 乐观锁（CAS 比较并交换）调整 SKU 库存并写变动日志。
    /// delta 可正（盈/增加）可负（亏/减少）。
    /// 以当前库存为期望值做条件更新，受影响行数=0 视为并发冲突并重试（最多 MaxCasRetry 次）。
    /// </summary>
    private async Task AdjustStockAndLogAsync(
        List<LogStockLog> logs,
        Dictionary<long, int> stockCache,
        long prodInfoId, long skuId, int delta,
        string remark, string opUser, long refId)
    {
        for (var attempt = 0; attempt < MaxCasRetry; attempt++)
        {
            // 取当前库存：优先用本事务已更新的缓存值，否则从库实时读取
            var current = stockCache.TryGetValue(skuId, out var cached)
                ? cached
                : await _db.ProdSkus.AsNoTracking()
                    .Where(s => s.Id == skuId)
                    .Select(s => s.Stock)
                    .FirstAsync();

            var after = current + delta;

            // 库存校验：调整后不得出现负库存
            if (after < 0)
            {
                throw new InvalidOperationException(
                    $"SKU(sku_id={skuId}) 调整后库存为负（当前 {current}，差值 {delta}）");
            }

            // 乐观锁：仅当库内 stock 仍等于读取到的 current 时才更新
            var affected = await _db.ProdSkus
                .Where(s => s.Id == skuId && s.Stock == current)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, after));

            if (affected == 1)
            {
                // 更新本事务库存缓存，供同 SKU 后续明细顺序计算
                stockCache[skuId] = after;

                logs.Add(new LogStockLog
                {
                    ProdInfoId = prodInfoId,
                    SkuId = skuId,
                    ChangeType = ChangeTypeCheck,
                    ChangeQty = delta,
                    BeforeStock = current,
                    AfterStock = after,
                    RefType = RefTypeCheck,
                    RefId = refId,
                    Remark = remark,
                    Operator = opUser
                });
                return;
            }
            // affected==0：被其他请求并发修改，循环重新读取最新库存后重试
        }

        throw new InvalidOperationException($"SKU(sku_id={skuId}) 库存更新并发冲突，请重试");
    }

    /// <summary>
    /// 生成盘点单号：PD + yyyyMMddHHmmss + 4位随机数（如 PD202609101430251234）。
    /// 极端情况下撞号（数据库 uk_check_no 唯一索引兜底）则重试，最多 5 次。
    /// </summary>
    private async Task<string> GenerateCheckNoAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var no = NoPrefix
                + DateTime.Now.ToString("yyyyMMddHHmmss")
                + Random.Shared.Next(0, 10000).ToString("D4");

            if (!await _checkRepository.CheckNoExistsAsync(no))
            {
                return no;
            }
        }

        throw new InvalidOperationException("盘点单号生成失败（号段冲突），请重试");
    }

    /// <summary>实体 → 列表 DTO（列表不带明细）</summary>
    private static StockCheckListDto MapToListDto(LogStockCheck o) => new()
    {
        Id = o.Id,
        CheckNo = o.CheckNo,
        Status = o.Status,
        Remark = o.Remark,
        Operator = o.Operator,
        CreateTime = o.CreateTime,
        UpdateTime = o.UpdateTime
    };

    /// <summary>明细实体 → DTO（仅标量字段，展示名称由调用方批量回填）</summary>
    private static StockCheckItemDto MapToItemDto(LogStockCheckItem i) => new()
    {
        Id = i.Id,
        CheckId = i.CheckId,
        ProdInfoId = i.ProdInfoId,
        SkuId = i.SkuId,
        SystemStock = i.SystemStock,
        ActualStock = i.ActualStock,
        DiffQty = i.DiffQty,
        Remark = i.Remark
    };
}
