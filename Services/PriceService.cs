using System.Text.Json;
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
/// 价格策略服务实现（A组）
///
/// 【取价优先级】客户专属价(2) &gt; 促销价(4) &gt; 客户等级价(1) &gt; 批量阶梯价(3) &gt; 商品标准售价。
///   严格按序执行，不可跳级；同类型内按 price_strategy.priority 降序取第一条命中规则。
///
/// 【底价保护】最终价低于 price_rule.min_price 时【不拦截】，仅回传 IsBelowMinPrice=true，
///   由前端或订单服务决定是否转人工审批。
///
/// 【可追溯】任何策略/规则写操作都写 price_change_log；下单时写 order_price_snapshot。
///
/// 【事务】策略提交/停用、规则批量维护均在同一事务内完成（业务数据与日志要么全成功要么全失败）。
/// </summary>
public class PriceService : IPriceService
{
    private const int MaxNoRetry = 5;

    /// <summary>discount_info JSON 序列化选项（与前端 camelCase 口径一致）</summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly IMemberLevelRepository _levelRepo;
    private readonly IPriceStrategyRepository _strategyRepo;
    private readonly IRepository<PriceRule> _ruleRepo;
    private readonly IRepository<PriceRuleItem> _ruleItemRepo;
    private readonly IRepository<PriceChangeLog> _changeLogRepo;
    private readonly IRepository<OrderPriceSnapshot> _snapshotRepo;
    private readonly IRepository<MemMember> _memberRepo;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly CreateMemberLevelValidator _createLevelValidator;
    private readonly UpdateMemberLevelValidator _updateLevelValidator;
    private readonly CreatePriceStrategyValidator _createStrategyValidator;
    private readonly UpdatePriceStrategyValidator _updateStrategyValidator;
    private readonly PriceRuleBatchSaveValidator _ruleBatchValidator;
    private readonly QuotationRequestValidator _quotationValidator;

    public PriceService(
        AppDbContext db,
        IMemberLevelRepository levelRepo,
        IPriceStrategyRepository strategyRepo,
        IRepository<PriceRule> ruleRepo,
        IRepository<PriceRuleItem> ruleItemRepo,
        IRepository<PriceChangeLog> changeLogRepo,
        IRepository<OrderPriceSnapshot> snapshotRepo,
        IRepository<MemMember> memberRepo,
        IMapper mapper,
        CurrentUserService currentUser,
        CreateMemberLevelValidator createLevelValidator,
        UpdateMemberLevelValidator updateLevelValidator,
        CreatePriceStrategyValidator createStrategyValidator,
        UpdatePriceStrategyValidator updateStrategyValidator,
        PriceRuleBatchSaveValidator ruleBatchValidator,
        QuotationRequestValidator quotationValidator)
    {
        _db = db;
        _levelRepo = levelRepo;
        _strategyRepo = strategyRepo;
        _ruleRepo = ruleRepo;
        _ruleItemRepo = ruleItemRepo;
        _changeLogRepo = changeLogRepo;
        _snapshotRepo = snapshotRepo;
        _memberRepo = memberRepo;
        _mapper = mapper;
        _currentUser = currentUser;
        _createLevelValidator = createLevelValidator;
        _updateLevelValidator = updateLevelValidator;
        _createStrategyValidator = createStrategyValidator;
        _updateStrategyValidator = updateStrategyValidator;
        _ruleBatchValidator = ruleBatchValidator;
        _quotationValidator = quotationValidator;
    }

    // ============================================================
    // 一、客户（会员）等级
    // ============================================================

    /// <summary>分页查询客户等级</summary>
    public async Task<ApiResponse<PagedResult<MemberLevelDto>>> GetLevelsAsync(MemberLevelQueryDto q)
    {
        NormalizePaging(q);
        var (items, total) = await _levelRepo.GetPagedListAsync(q);

        var dtos = items.Select(l =>
        {
            var d = _mapper.Map<MemberLevelDto>(l);
            d.StatusText = l.Status == 1 ? "启用" : "停用";
            return d;
        }).ToList();

        return ApiResponse<PagedResult<MemberLevelDto>>.Success(
            PagedResult<MemberLevelDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    /// <summary>新增客户等级（level_code 唯一性校验）</summary>
    public async Task<ApiResponse<long>> CreateLevelAsync(CreateMemberLevelDto dto)
    {
        _createLevelValidator.ValidateAndThrow(dto);

        if (await _levelRepo.ExistsLevelCodeAsync(dto.LevelCode))
        {
            return ApiResponse<long>.Fail($"等级编码 {dto.LevelCode} 已存在");
        }

        var entity = new MemMemberLevel
        {
            LevelName = dto.LevelName.Trim(),
            LevelCode = dto.LevelCode.Trim(),
            SortOrder = dto.SortOrder,
            DefaultDiscount = decimal.Round(dto.DefaultDiscount, 4),
            Description = dto.Description,
            Status = dto.Status
        };

        await _levelRepo.AddAsync(entity);
        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "客户等级创建成功");
    }

    /// <summary>更新客户等级</summary>
    public async Task<ApiResponse<bool>> UpdateLevelAsync(long id, UpdateMemberLevelDto dto)
    {
        _updateLevelValidator.ValidateAndThrow(dto);

        var entity = await _levelRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("客户等级不存在", 404);
        }

        if (await _levelRepo.ExistsLevelCodeAsync(dto.LevelCode.Trim(), id))
        {
            return ApiResponse<bool>.Fail($"等级编码 {dto.LevelCode} 已被其他等级占用");
        }

        entity.LevelName = dto.LevelName.Trim();
        entity.LevelCode = dto.LevelCode.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.DefaultDiscount = decimal.Round(dto.DefaultDiscount, 4);
        entity.Description = dto.Description;
        entity.Status = dto.Status;

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Success(true, "客户等级已更新");
    }

    /// <summary>
    /// 启用/停用客户等级。
    /// 【停用约束】若仍存在引用该等级的「启用中且生效期内」价格规则，禁止停用，
    /// 防止客户下单时价格静默跳回原价。
    /// </summary>
    public async Task<ApiResponse<bool>> UpdateLevelStatusAsync(long id, MemberLevelStatusDto dto)
    {
        if (dto.Status != 0 && dto.Status != 1)
        {
            return ApiResponse<bool>.Fail("状态非法（0-停用 1-启用）");
        }

        var entity = await _levelRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("客户等级不存在", 404);
        }

        if (dto.Status == 0 && await _levelRepo.HasEnabledRulesAsync(id))
        {
            return ApiResponse<bool>.Fail("该等级仍被启用中的价格策略引用，请先停用或调整相关策略");
        }

        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, dto.Status == 1 ? "等级已启用" : "等级已停用");
    }

    // ============================================================
    // 二、价格策略
    // ============================================================

    /// <summary>分页查询价格策略（补全状态文案、策略类型文案、规则条数、创建人）</summary>
    public async Task<ApiResponse<PagedResult<PriceStrategyDto>>> GetStrategiesAsync(PriceStrategyQueryDto q)
    {
        NormalizePaging(q);
        var (items, total) = await _strategyRepo.GetPagedListAsync(q);

        var ids = items.Select(s => s.Id).ToList();
        var ruleCountMap = await _strategyRepo.CountRulesAsync(ids);
        var userMap = await GetUserNameMapAsync(items.Select(s => s.CreateBy).ToList());

        var dtos = items.Select(s =>
        {
            var d = _mapper.Map<PriceStrategyDto>(s);
            d.StrategyTypeText = StrategyTypeHelper.GetName(s.StrategyType);
            d.StatusText = StrategyStatusHelper.GetName(s.Status);
            d.RuleCount = ruleCountMap.TryGetValue(s.Id, out var c) ? c : 0;
            d.CreateByName = userMap.TryGetValue(s.CreateBy, out var n) ? n : null;
            return d;
        }).ToList();

        return ApiResponse<PagedResult<PriceStrategyDto>>.Success(
            PagedResult<PriceStrategyDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    /// <summary>新增价格策略（默认草稿，自动生成 strategy_no，写变更日志）</summary>
    public async Task<ApiResponse<long>> CreateStrategyAsync(CreatePriceStrategyDto dto)
    {
        _createStrategyValidator.ValidateAndThrow(dto);

        var now = DateTime.Now;
        PriceStrategy? entity = null;

        for (var i = 0; i < MaxNoRetry; i++)
        {
            var candidate = _strategyRepo.GenerateStrategyNo();
            if (await _strategyRepo.ExistsStrategyNoAsync(candidate))
            {
                continue;
            }

            entity = new PriceStrategy
            {
                StrategyNo = candidate,
                StrategyName = dto.StrategyName.Trim(),
                StrategyType = dto.StrategyType,
                Priority = dto.Priority,
                EffectiveDate = dto.EffectiveDate,
                ExpireDate = dto.ExpireDate,
                Status = (int)StrategyStatus.Draft,   // 默认草稿
                Description = dto.Description,
                CreateBy = _currentUser.UserId
            };

            await _strategyRepo.AddAsync(entity);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                entity = null;   // 极小概率并发撞号，重试
            }
        }

        if (entity == null)
        {
            return ApiResponse<long>.Fail("策略编号生成失败，请稍后重试");
        }

        // 新增留痕
        await AppendChangeLogAsync(entity.Id, null, PriceOperationType.Create, "ALL",
            null, $"{entity.StrategyName}/{StrategyTypeHelper.GetName(entity.StrategyType)}", "新增策略", now);
        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "策略创建成功（草稿）");
    }

    /// <summary>更新策略基本信息（仅草稿可改，逐字段记录变更日志）</summary>
    public async Task<ApiResponse<bool>> UpdateStrategyAsync(long id, UpdatePriceStrategyDto dto)
    {
        _updateStrategyValidator.ValidateAndThrow(dto);

        var entity = await _strategyRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("价格策略不存在", 404);
        }

        if (entity.Status != (int)StrategyStatus.Draft)
        {
            return ApiResponse<bool>.Fail("仅草稿状态的策略可以修改");
        }

        var now = DateTime.Now;

        if (entity.StrategyName != dto.StrategyName.Trim())
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "strategy_name",
                entity.StrategyName, dto.StrategyName.Trim(), null, now);
            entity.StrategyName = dto.StrategyName.Trim();
        }
        if (entity.StrategyType != dto.StrategyType)
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "strategy_type",
                StrategyTypeHelper.GetName(entity.StrategyType), StrategyTypeHelper.GetName(dto.StrategyType), null, now);
            entity.StrategyType = dto.StrategyType;
        }
        if (entity.Priority != dto.Priority)
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "priority",
                entity.Priority.ToString(), dto.Priority.ToString(), null, now);
            entity.Priority = dto.Priority;
        }
        if (entity.EffectiveDate != dto.EffectiveDate)
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "effective_date",
                entity.EffectiveDate.ToString("yyyy-MM-dd HH:mm:ss"), dto.EffectiveDate.ToString("yyyy-MM-dd HH:mm:ss"), null, now);
            entity.EffectiveDate = dto.EffectiveDate;
        }
        if (entity.ExpireDate != dto.ExpireDate)
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "expire_date",
                entity.ExpireDate.ToString("yyyy-MM-dd HH:mm:ss"), dto.ExpireDate.ToString("yyyy-MM-dd HH:mm:ss"), null, now);
            entity.ExpireDate = dto.ExpireDate;
        }
        if (entity.Description != dto.Description)
        {
            await AppendChangeLogAsync(id, null, PriceOperationType.Update, "description",
                entity.Description, dto.Description, null, now);
            entity.Description = dto.Description;
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Success(true, "策略已更新");
    }

    /// <summary>
    /// 草稿提交启用（校验规则完整性）：
    ///   1) 至少一条启用状态的规则；
    ///   2) 客户专属价必须指定会员、客户等级价必须指定等级；
    ///   3) 指定分类必须填分类、指定商品必须至少一条明细；
    ///   4) 折扣率必须在 0~1 之间、固定价必须大于 0。
    /// </summary>
    public async Task<ApiResponse<bool>> SubmitStrategyAsync(long id)
    {
        var entity = await _strategyRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("价格策略不存在", 404);
        }

        if (entity.Status != (int)StrategyStatus.Draft)
        {
            return ApiResponse<bool>.Fail("仅草稿状态的策略可以提交启用");
        }

        var rules = await _ruleRepo.GetListAsync(r => r.StrategyId == id);
        var enabled = rules.Where(r => r.Status == 1).ToList();
        if (enabled.Count == 0)
        {
            return ApiResponse<bool>.Fail("策略下没有任何启用状态的规则，无法提交");
        }

        var ruleIds = enabled.Select(r => r.Id).ToList();
        var items = await _ruleItemRepo.GetListAsync(i => ruleIds.Contains(i.RuleId));

        foreach (var rule in enabled)
        {
            var error = ValidateRuleIntegrity(entity.StrategyType, rule, items.Where(i => i.RuleId == rule.Id).ToList());
            if (error != null)
            {
                return ApiResponse<bool>.Fail($"规则(ID={rule.Id})校验不通过：{error}");
            }
        }

        var now = DateTime.Now;
        entity.Status = (int)StrategyStatus.Enabled;
        await AppendChangeLogAsync(id, null, PriceOperationType.Update, "status",
            StrategyStatusHelper.GetName((int)StrategyStatus.Draft),
            StrategyStatusHelper.GetName((int)StrategyStatus.Enabled), "提交启用", now);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "策略已提交启用");
    }

    /// <summary>停用策略（记录变更日志）</summary>
    public async Task<ApiResponse<bool>> DisableStrategyAsync(long id, string? reason)
    {
        var entity = await _strategyRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("价格策略不存在", 404);
        }

        if (entity.Status == (int)StrategyStatus.Disabled)
        {
            return ApiResponse<bool>.Success(true, "该策略已是停用状态");
        }

        var now = DateTime.Now;
        var oldStatus = entity.Status;
        entity.Status = (int)StrategyStatus.Disabled;

        await AppendChangeLogAsync(id, null, PriceOperationType.Disable, "status",
            StrategyStatusHelper.GetName(oldStatus),
            StrategyStatusHelper.GetName((int)StrategyStatus.Disabled), reason ?? "停用策略", now);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "策略已停用");
    }

    /// <summary>
    /// 删除策略（仅草稿）：
    /// 草稿未参与取价，可安全物理删除；其下规则与明细一并删除（明细无外部引用），
    /// 但 price_change_log 审计记录【保留】，保证"谁改过价"始终可追溯。
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteStrategyAsync(long id)
    {
        var entity = await _strategyRepo.GetByIdTrackedAsync(id);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("价格策略不存在", 404);
        }
        if (entity.Status != (int)StrategyStatus.Draft)
        {
            return ApiResponse<bool>.Fail("仅草稿状态的策略可以删除（启用/停用的策略请走停用流程，保留历史取价痕迹）");
        }

        var now = DateTime.Now;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var ruleIds = await _db.PriceRules.AsNoTracking()
                .Where(r => r.StrategyId == id)
                .Select(r => r.Id)
                .ToListAsync();

            if (ruleIds.Count > 0)
            {
                var items = await _db.PriceRuleItems.Where(i => ruleIds.Contains(i.RuleId)).ToListAsync();
                foreach (var item in items)
                {
                    _ruleItemRepo.Remove(item);
                }

                var rules = await _db.PriceRules.Where(r => r.StrategyId == id).ToListAsync();
                foreach (var rule in rules)
                {
                    await AppendChangeLogAsync(id, rule.Id, PriceOperationType.Delete, "ALL",
                        DescribeRule(rule), null, "删除草稿策略，规则一并移除", now);
                    _ruleRepo.Remove(rule);
                }
            }

            await AppendChangeLogAsync(id, null, PriceOperationType.Delete, "ALL",
                $"{entity.StrategyName}/{StrategyTypeHelper.GetName(entity.StrategyType)}", null, "删除草稿策略", now);

            _strategyRepo.Remove(entity);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<bool>.Success(true, "草稿策略已删除");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // 三、价格规则（批量维护：全量覆盖）
    // ============================================================

    /// <summary>查询策略下的规则及明细</summary>
    public async Task<ApiResponse<List<PriceRuleDto>>> GetRulesAsync(long strategyId)
    {
        var exists = await _strategyRepo.AnyAsync(s => s.Id == strategyId);
        if (!exists)
        {
            return ApiResponse<List<PriceRuleDto>>.Fail("价格策略不存在", 404);
        }

        var rules = await _ruleRepo.GetListAsync(r => r.StrategyId == strategyId);
        if (rules.Count == 0)
        {
            return ApiResponse<List<PriceRuleDto>>.Success(new List<PriceRuleDto>());
        }

        var ruleIds = rules.Select(r => r.Id).ToList();
        var items = await _ruleItemRepo.GetListAsync(i => ruleIds.Contains(i.RuleId));

        var dtos = await MapRuleDtosAsync(rules, items);
        return ApiResponse<List<PriceRuleDto>>.Success(dtos);
    }

    /// <summary>
    /// 批量维护规则及明细（全量覆盖，事务内执行）：
    ///   1) 入参中带 ID 的规则 → 更新（逐字段记录变更日志，明细全量重建）；
    ///   2) 入参中无 ID 的规则 → 新增（记录新增日志）；
    ///   3) 库中未被提交的旧规则 → 停用（不做物理删除，保留 order_price_snapshot 的可追溯性）。
    /// </summary>
    public async Task<ApiResponse<bool>> SaveRulesBatchAsync(long strategyId, PriceRuleBatchSaveDto dto)
    {
        _ruleBatchValidator.ValidateAndThrow(dto);

        var strategy = await _strategyRepo.GetByIdTrackedAsync(strategyId);
        if (strategy == null)
        {
            return ApiResponse<bool>.Fail("价格策略不存在", 404);
        }
        if (strategy.Status != (int)StrategyStatus.Draft)
        {
            return ApiResponse<bool>.Fail("仅草稿状态的策略可以维护规则（启用后请先停用）");
        }

        var now = DateTime.Now;

        // price_rule_item.prod_info_id 是 NOT NULL 列：一次性把本次涉及 SKU 反查成商品ID，
        // 避免逐行查库，也保证明细行能落库。
        var allSkuIds = dto.Rules
            .SelectMany(r => r.Items)
            .Select(i => i.MaterialId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var prodIdMap = allSkuIds.Count == 0
            ? new Dictionary<long, long>()
            : await _db.ProdSkus.AsNoTracking()
                .Where(s => allSkuIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.ProdInfoId);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var existings = await _db.PriceRules.Where(r => r.StrategyId == strategyId).ToListAsync();
            var existingIds = existings.Select(r => r.Id).ToList();
            var existingItems = existingIds.Count == 0
                ? new List<PriceRuleItem>()
                : await _db.PriceRuleItems.Where(i => existingIds.Contains(i.RuleId)).ToListAsync();

            var keepIds = new List<long>();

            foreach (var input in dto.Rules)
            {
                PriceRule rule;
                if (input.Id.HasValue && input.Id.Value > 0 && existings.Any(r => r.Id == input.Id.Value))
                {
                    rule = existings.First(r => r.Id == input.Id.Value);
                    await ApplyRuleUpdateAsync(rule, input, dto.Reason, now);
                }
                else
                {
                    rule = new PriceRule
                    {
                        StrategyId = strategyId,
                        MemMemberId = input.MemMemberId,
                        CustomerLevelId = input.CustomerLevelId,
                        ApplyScope = input.ApplyScope,
                        CategoryId = input.CategoryId,
                        CalcType = input.CalcType,
                        CalcValue = decimal.Round(input.CalcValue, 4),
                        MinPrice = input.MinPrice.HasValue ? decimal.Round(input.MinPrice.Value, 2) : null,
                        Status = input.Status
                    };
                    await _ruleRepo.AddAsync(rule);
                    await _db.SaveChangesAsync();   // 先落库拿到自增 rule.Id

                    await AppendChangeLogAsync(strategyId, rule.Id, PriceOperationType.Create, "ALL",
                        null, DescribeRule(rule), dto.Reason ?? "批量维护新增规则", now);
                }

                keepIds.Add(rule.Id);

                // 明细全量重建：先删旧再插新
                var oldItems = existingItems.Where(i => i.RuleId == rule.Id).ToList();
                foreach (var old in oldItems)
                {
                    _ruleItemRepo.Remove(old);
                }

                foreach (var it in input.Items)
                {
                    await _ruleItemRepo.AddAsync(new PriceRuleItem
                    {
                        RuleId = rule.Id,
                        MaterialId = it.MaterialId,
                        ProdInfoId = prodIdMap.TryGetValue(it.MaterialId, out var pid) ? pid : 0,
                        TierNo = it.TierNo <= 0 ? 1 : it.TierNo,
                        MinQuantity = it.MinQuantity,
                        MaxQuantity = it.MaxQuantity,
                        Price = it.Price.HasValue ? decimal.Round(it.Price.Value, 2) : null,
                        Discount = it.Discount.HasValue ? decimal.Round(it.Discount.Value, 4) : null,
                        ReduceAmount = it.ReduceAmount.HasValue ? decimal.Round(it.ReduceAmount.Value, 2) : null
                    });
                }
            }

            // 未被提交的旧规则 → 停用（保留历史快照可追溯）
            foreach (var old in existings.Where(r => !keepIds.Contains(r.Id)).ToList())
            {
                if (old.Status == 0)
                {
                    continue;
                }
                old.Status = 0;
                await AppendChangeLogAsync(strategyId, old.Id, PriceOperationType.Disable, "status",
                    "1", "0", dto.Reason ?? "批量维护中未提交，自动停用", now);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<bool>.Success(true, "规则批量维护成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 商品物料（SKU）下拉搜索：价格规则「指定商品 / 阶梯价」明细行选商品时使用。
    /// 只返回启用状态的 SKU，避免把已下架商品配进价格策略。
    /// </summary>
    public async Task<ApiResponse<List<MaterialOptionDto>>> GetMaterialsAsync(string? keyword, int limit = 50)
    {
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        var query = _db.ProdSkus.AsNoTracking().Where(s => s.Status == 1);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(s =>
                (s.SkuName != null && s.SkuName.Contains(kw))
                || s.SkuCode.Contains(kw)
                || _db.ProdInfos.Any(p => p.Id == s.ProdInfoId && p.ProdInfoName.Contains(kw)));
        }

        var skus = await query
            .OrderByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync();

        if (skus.Count == 0)
        {
            return ApiResponse<List<MaterialOptionDto>>.Success(new List<MaterialOptionDto>());
        }

        var prodIds = skus.Select(s => s.ProdInfoId).Distinct().ToList();
        var prodMap = await _db.ProdInfos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var options = skus.Select(s =>
        {
            prodMap.TryGetValue(s.ProdInfoId, out var prod);
            return new MaterialOptionDto
            {
                MaterialId = s.Id,
                ProdInfoId = s.ProdInfoId,
                ProdName = prod?.ProdInfoName,
                SkuName = s.SkuName,
                SkuCode = s.SkuCode,
                RetailPrice = s.RetailPrice,
                ProdCategoryId = prod?.ProdCategoryId ?? 0
            };
        }).ToList();

        return ApiResponse<List<MaterialOptionDto>>.Success(options);
    }

    // ============================================================
    // 四、取价引擎
    // ============================================================

    /// <summary>
    /// 核心取价：入参会员 + 商品清单（物料ID + 数量），按优先级逐层匹配，
    /// 返回每个商品的最终成交价、命中策略/规则、计算明细与底价保护标志。
    /// </summary>
    public async Task<ApiResponse<List<QuotationResultDto>>> QuotationAsync(QuotationRequestDto dto)
    {
        _quotationValidator.ValidateAndThrow(dto);

        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == dto.MemMemberId && m.IsDeleted == 0);
        if (member == null)
        {
            return ApiResponse<List<QuotationResultDto>>.Fail("会员不存在", 404);
        }

        // ---- 1) 一次性加载商品 / SKU / 生效策略 / 规则 / 明细，避免逐行查库 ----
        var skuIds = dto.Items.Select(i => i.MaterialId).Distinct().ToList();
        var skuMap = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var prodIds = skuMap.Values.Select(s => s.ProdInfoId).Distinct().ToList();
        var prodMap = await _db.ProdInfos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var now = DateTime.Now;
        var strategies = await _strategyRepo.GetEffectiveStrategiesAsync(now);
        var strategyIds = strategies.Select(s => s.Id).ToList();
        var rules = strategyIds.Count == 0
            ? new List<PriceRule>()
            : await _db.PriceRules.AsNoTracking()
                .Where(r => strategyIds.Contains(r.StrategyId) && r.Status == 1)
                .ToListAsync();

        var ruleIds = rules.Select(r => r.Id).ToList();
        var ruleItemMap = ruleIds.Count == 0
            ? new Dictionary<long, List<PriceRuleItem>>()
            : (await _db.PriceRuleItems.AsNoTracking()
                .Where(i => ruleIds.Contains(i.RuleId))
                .ToListAsync())
              .GroupBy(i => i.RuleId)
              .ToDictionary(g => g.Key, g => g.OrderBy(i => i.TierNo).ToList());

        var strategyMap = strategies.ToDictionary(s => s.Id);

        // 等级默认折扣（未命中任何策略时的兜底）
        decimal? levelDefaultDiscount = null;
        string? levelName = null;
        long? levelId = member.CustomerLevelId;
        if (member.CustomerLevelId.HasValue)
        {
            var level = await _db.MemMemberLevels.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == member.CustomerLevelId.Value && l.Status == 1);
            if (level != null)
            {
                levelDefaultDiscount = level.DefaultDiscount;
                levelName = level.LevelName;
            }
        }

        // ---- 2) 逐商品取价 ----
        var results = new List<QuotationResultDto>();

        foreach (var input in dto.Items)
        {
            if (!skuMap.TryGetValue(input.MaterialId, out var sku))
            {
                throw new InvalidOperationException($"商品物料不存在（material_id={input.MaterialId}）");
            }

            prodMap.TryGetValue(sku.ProdInfoId, out var prod);
            var standard = sku.RetailPrice;
            var quantity = input.Quantity <= 0 ? 1 : input.Quantity;

            var result = new QuotationResultDto
            {
                MaterialId = sku.Id,
                ProdInfoId = sku.ProdInfoId,
                MaterialName = prod?.ProdInfoName,
                SkuName = sku.SkuName,
                Quantity = quantity,
                StandardPrice = standard,
                FinalPrice = standard,
                Subtotal = decimal.Round(standard * quantity, 2),
                MatchedStrategyType = StrategyPriority.None,
                MatchedStrategyTypeText = "标准售价",
                DiscountInfo = new DiscountInfoDto
                {
                    StandardPrice = standard,
                    FinalPrice = standard,
                    DiscountAmount = 0m,
                    Remark = "未命中任何价格策略，按商品标准售价"
                }
            };

            // 按业务优先级逐层匹配，命中即停（不可跳级）
            var matched = false;
            foreach (var type in StrategyPriority.Order)
            {
                // 同类型内按 priority 降序（仓储已排好序，这里保持顺序即可）
                foreach (var strategy in strategies.Where(s => s.StrategyType == type))
                {
                    var rule = MatchRule(strategy, rules, ruleItemMap, member, prod, sku.Id, quantity);
                    if (rule == null)
                    {
                        continue;
                    }

                    ApplyMatchedRule(result, strategy, rule, ruleItemMap.TryGetValue(rule.Id, out var items) ? items : new List<PriceRuleItem>(), quantity);
                    matched = true;
                    break;
                }
                if (matched)
                {
                    break;
                }
            }

            // 兜底：等级默认折扣（仅在完全没命中策略时生效，且折扣率 < 1）
            if (!matched && levelDefaultDiscount.HasValue && levelDefaultDiscount.Value > 0 && levelDefaultDiscount.Value < 1)
            {
                var final = decimal.Round(standard * levelDefaultDiscount.Value, 2);
                result.FinalPrice = final;
                result.Subtotal = decimal.Round(final * quantity, 2);
                result.MatchedStrategyType = (int)StrategyType.MemberLevel;
                result.MatchedStrategyTypeText = "客户等级价";
                result.MatchedStrategyName = $"{levelName}（默认折扣）";
                result.CalcType = (int)CalcType.Discount;
                result.CalcTypeText = CalcTypeHelper.GetName((int)CalcType.Discount);
                result.CalcValue = levelDefaultDiscount.Value;
                result.DiscountInfo = new DiscountInfoDto
                {
                    StrategyId = null,
                    StrategyName = result.MatchedStrategyName,
                    StrategyType = (int)StrategyType.MemberLevel,
                    StrategyTypeText = "客户等级价",
                    RuleId = null,
                    CalcTypeText = "折扣率",
                    CalcValue = levelDefaultDiscount.Value,
                    StandardPrice = standard,
                    FinalPrice = final,
                    DiscountAmount = decimal.Round(standard - final, 2),
                    Remark = $"等级默认折扣 {levelDefaultDiscount.Value:0.####}"
                };
            }

            // 最终兜底：标准售价时也要保证小计一致
            result.Subtotal = decimal.Round(result.FinalPrice * quantity, 2);
            results.Add(result);
        }

        return ApiResponse<List<QuotationResultDto>>.Success(results);
    }

    /// <summary>
    /// 规则匹配：先按策略类型校验"对谁生效"，再按适用范围校验"对哪些商品生效"。
    /// </summary>
    private static PriceRule? MatchRule(
        PriceStrategy strategy,
        List<PriceRule> rules,
        Dictionary<long, List<PriceRuleItem>> ruleItemMap,
        MemMember member,
        ProdInfo? prod,
        long materialId,
        int quantity)
    {
        var candidates = rules.Where(r => r.StrategyId == strategy.Id).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var rule in candidates)
        {
            // 1) 对谁生效
            if (strategy.StrategyType == (int)StrategyType.MemberExclusive)
            {
                if (!rule.MemMemberId.HasValue || rule.MemMemberId.Value != member.Id)
                {
                    continue;
                }
            }
            else if (strategy.StrategyType == (int)StrategyType.MemberLevel)
            {
                if (!member.CustomerLevelId.HasValue
                    || !rule.CustomerLevelId.HasValue
                    || rule.CustomerLevelId.Value != member.CustomerLevelId.Value)
                {
                    continue;
                }
            }

            // 2) 对哪些商品生效
            var items = ruleItemMap.TryGetValue(rule.Id, out var list) ? list : new List<PriceRuleItem>();
            switch (rule.ApplyScope)
            {
                case (int)ApplyScope.All:
                    break;

                case (int)ApplyScope.Category:
                    if (prod == null || !rule.CategoryId.HasValue || rule.CategoryId.Value != prod.ProdCategoryId)
                    {
                        continue;
                    }
                    break;

                case (int)ApplyScope.Material:
                    if (!items.Any(i => i.MaterialId == materialId))
                    {
                        continue;
                    }
                    break;

                default:
                    continue;
            }

            // 3) 批量阶梯价：必须命中数量阶梯（无阶梯配置则不生效）
            if (strategy.StrategyType == (int)StrategyType.Tiered)
            {
                var tier = FindTier(items, materialId, quantity);
                if (tier == null)
                {
                    continue;
                }
            }

            return rule;
        }

        return null;
    }

    /// <summary>在阶梯明细中命中数量区间（max_quantity 为空或 0 视为不封顶）</summary>
    private static PriceRuleItem? FindTier(List<PriceRuleItem> items, long materialId, int quantity)
    {
        return items
            .Where(i => i.MaterialId == materialId
                        && i.MinQuantity <= quantity
                        && (!i.MaxQuantity.HasValue || i.MaxQuantity.Value <= 0 || i.MaxQuantity.Value >= quantity))
            .OrderBy(i => i.TierNo)
            .FirstOrDefault();
    }

    /// <summary>把命中规则的计算结果写入取价结果（含底价保护判定）</summary>
    private static void ApplyMatchedRule(
        QuotationResultDto result,
        PriceStrategy strategy,
        PriceRule rule,
        List<PriceRuleItem> items,
        int quantity)
    {
        var standard = result.StandardPrice;
        var calcType = rule.CalcType;
        var calcValue = rule.CalcValue;
        PriceRuleItem? tier = null;

        // 阶梯价优先取阶梯行上的值
        if (strategy.StrategyType == (int)StrategyType.Tiered)
        {
            tier = FindTier(items, result.MaterialId, quantity);
            if (tier != null)
            {
                if (calcType == (int)CalcType.Fixed && tier.Price.HasValue)
                {
                    calcValue = tier.Price.Value;
                }
                else if (calcType == (int)CalcType.Discount && tier.Discount.HasValue)
                {
                    calcValue = tier.Discount.Value;
                }
                else if (calcType == (int)CalcType.Reduce && tier.ReduceAmount.HasValue)
                {
                    calcValue = tier.ReduceAmount.Value;
                }
            }
        }

        var final = calcType switch
        {
            (int)CalcType.Fixed => calcValue,
            (int)CalcType.Discount => standard * calcValue,
            (int)CalcType.Reduce => standard - calcValue,
            _ => standard
        };
        if (final < 0) final = 0;
        final = decimal.Round(final, 2);

        // 底价保护：不拦截，只打标
        var belowMin = rule.MinPrice.HasValue && final < rule.MinPrice.Value;

        result.FinalPrice = final;
        result.Subtotal = decimal.Round(final * quantity, 2);
        result.MatchedStrategyId = strategy.Id;
        result.MatchedStrategyNo = strategy.StrategyNo;
        result.MatchedStrategyName = strategy.StrategyName;
        result.MatchedStrategyType = strategy.StrategyType;
        result.MatchedStrategyTypeText = StrategyTypeHelper.GetName(strategy.StrategyType);
        result.MatchedRuleId = rule.Id;
        result.MatchedTierNo = tier?.TierNo;
        result.CalcType = calcType;
        result.CalcTypeText = CalcTypeHelper.GetName(calcType);
        result.CalcValue = calcValue;
        result.MinPrice = rule.MinPrice;
        result.IsBelowMinPrice = belowMin;

        result.DiscountInfo = new DiscountInfoDto
        {
            StrategyId = strategy.Id,
            StrategyName = strategy.StrategyName,
            StrategyType = strategy.StrategyType,
            StrategyTypeText = StrategyTypeHelper.GetName(strategy.StrategyType),
            RuleId = rule.Id,
            TierNo = tier?.TierNo,
            CalcTypeText = CalcTypeHelper.GetName(calcType),
            CalcValue = calcValue,
            StandardPrice = standard,
            FinalPrice = final,
            DiscountAmount = decimal.Round(standard - final, 2),
            IsBelowMinPrice = belowMin,
            MinPrice = rule.MinPrice,
            Remark = belowMin
                ? $"{StrategyTypeHelper.GetName(strategy.StrategyType)}-{CalcTypeHelper.GetName(calcType)} {calcValue:0.####}（低于底价 {rule.MinPrice:0.00}，需人工审批）"
                : $"{StrategyTypeHelper.GetName(strategy.StrategyType)}-{CalcTypeHelper.GetName(calcType)} {calcValue:0.####}"
        };
    }

    // ============================================================
    // 五、订单价格快照
    // ============================================================

    /// <summary>内部接口：下单时批量写入价格快照（与订单创建同事务调用）</summary>
    public async Task<ApiResponse<bool>> SaveSnapshotsAsync(OrderPriceSnapshotBatchDto dto)
    {
        if (dto.OrderId <= 0)
        {
            return ApiResponse<bool>.Fail("订单ID不合法");
        }
        if (dto.Items.Count == 0)
        {
            return ApiResponse<bool>.Fail("快照明细不能为空");
        }

        // order_price_snapshot.prod_info_id 是 NOT NULL 列，需按 SKU 反查商品ID后一并写入
        var skuIds = dto.Items.Select(i => i.MaterialId).Distinct().ToList();
        var prodIdMap = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.ProdInfoId);

        foreach (var it in dto.Items)
        {
            await _snapshotRepo.AddAsync(new OrderPriceSnapshot
            {
                OrderId = dto.OrderId,
                OrderItemId = it.OrderItemId,
                MaterialId = it.MaterialId,
                ProdInfoId = prodIdMap.TryGetValue(it.MaterialId, out var pid) ? pid : 0,
                StandardPrice = decimal.Round(it.StandardPrice, 2),
                MatchedStrategyId = it.MatchedStrategyId,
                MatchedStrategyType = it.MatchedStrategyType,
                MatchedRuleId = it.MatchedRuleId,
                FinalPrice = decimal.Round(it.FinalPrice, 2),
                DiscountInfo = it.DiscountInfo
            });
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Success(true, "价格快照写入成功");
    }

    /// <summary>按订单查询价格快照</summary>
    public async Task<ApiResponse<List<OrderPriceSnapshotDto>>> GetSnapshotsAsync(long orderId)
    {
        var list = await _snapshotRepo.GetListAsync(s => s.OrderId == orderId);
        var dtos = list.Select(s =>
        {
            var d = _mapper.Map<OrderPriceSnapshotDto>(s);
            d.MatchedStrategyTypeText = s.MatchedStrategyType.HasValue && s.MatchedStrategyType.Value > 0
                ? StrategyTypeHelper.GetName(s.MatchedStrategyType.Value)
                : "标准售价";
            return d;
        }).ToList();

        return ApiResponse<List<OrderPriceSnapshotDto>>.Success(dtos);
    }

    /// <summary>
    /// 分页查询订单价格快照（历史价格追溯）。
    /// 关联 trx_order（订单号/下单时间）、mem_member（客户）、prod_sku（商品名）、price_strategy（策略名），
    /// 回答"这笔成交价从哪条策略来的"。
    /// </summary>
    public async Task<ApiResponse<PagedResult<OrderPriceSnapshotDto>>> GetSnapshotsPagedAsync(OrderPriceSnapshotQueryDto q)
    {
        NormalizePaging(q);

        // 主表 left join 订单（快照由下单写入，订单必然存在；用 join 以便按订单号/时间筛选）
        var query = from s in _db.OrderPriceSnapshots.AsNoTracking()
                    join o in _db.TrxOrders.AsNoTracking() on s.OrderId equals o.Id into oj
                    from o in oj.DefaultIfEmpty()
                    select new { s, o };

        if (!string.IsNullOrWhiteSpace(q.OrderNo))
        {
            var no = q.OrderNo.Trim();
            query = query.Where(x => x.o != null && x.o.OrderNo.Contains(no));
        }

        if (!string.IsNullOrWhiteSpace(q.MemberName))
        {
            var kw = q.MemberName.Trim();
            query = query.Where(x => x.o != null
                && _db.MemMembers.Any(m => m.Id == x.o.BuyerId && m.Name.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(q.MaterialName))
        {
            var kw = q.MaterialName.Trim();
            query = query.Where(x =>
                _db.ProdSkus.Any(sk => sk.Id == x.s.MaterialId && (sk.SkuName!.Contains(kw) || sk.SkuCode.Contains(kw))));
        }

        if (q.MatchedStrategyType.HasValue)
        {
            query = query.Where(x => x.s.MatchedStrategyType == q.MatchedStrategyType.Value);
        }

        if (q.StartTime.HasValue)
        {
            query = query.Where(x => x.o != null && x.o.CreateTime >= q.StartTime.Value);
        }
        if (q.EndTime.HasValue)
        {
            query = query.Where(x => x.o != null && x.o.CreateTime < q.EndTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(x => x.s.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        if (rows.Count == 0)
        {
            return ApiResponse<PagedResult<OrderPriceSnapshotDto>>.Success(
                PagedResult<OrderPriceSnapshotDto>.Create(new List<OrderPriceSnapshotDto>(), 0, q.Page, q.PageSize));
        }

        // 批量补全：客户名 / 商品名 / 策略名（避免 N+1）
        var buyerIds = rows.Select(x => x.o?.BuyerId ?? 0).Where(x => x > 0).Distinct().ToList();
        var memberMap = buyerIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.MemMembers.AsNoTracking()
                .Where(m => buyerIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name);

        var materialIds = rows.Select(x => x.s.MaterialId).Distinct().ToList();
        var skuMap = await _db.ProdSkus.AsNoTracking()
            .Where(sk => materialIds.Contains(sk.Id))
            .ToDictionaryAsync(sk => sk.Id, sk => sk.SkuName ?? string.Empty);

        var strategyIds = rows.Select(x => x.s.MatchedStrategyId).Where(x => x.HasValue && x.Value > 0)
            .Select(x => x!.Value).Distinct().ToList();
        var strategyMap = strategyIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.PriceStrategies.AsNoTracking()
                .Where(s => strategyIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.StrategyName);

        var dtos = rows.Select(x =>
        {
            var d = _mapper.Map<OrderPriceSnapshotDto>(x.s);
            d.OrderNo = x.o?.OrderNo;
            d.OrderTime = x.o?.CreateTime;
            d.MemberName = (x.o != null && memberMap.TryGetValue(x.o.BuyerId, out var mn)) ? mn : null;
            d.MaterialName = skuMap.TryGetValue(x.s.MaterialId, out var skn) ? skn : null;
            d.MatchedStrategyName = (x.s.MatchedStrategyId.HasValue
                                     && strategyMap.TryGetValue(x.s.MatchedStrategyId.Value, out var sn)) ? sn : null;
            d.MatchedStrategyTypeText = x.s.MatchedStrategyType.HasValue && x.s.MatchedStrategyType.Value > 0
                ? StrategyTypeHelper.GetName(x.s.MatchedStrategyType.Value)
                : "标准售价";
            return d;
        }).ToList();

        return ApiResponse<PagedResult<OrderPriceSnapshotDto>>.Success(
            PagedResult<OrderPriceSnapshotDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    /// <summary>序列化折扣详情为 JSON（供 OrderService 写快照复用）</summary>
    public static string SerializeDiscountInfo(DiscountInfoDto info)
        => JsonSerializer.Serialize(info, JsonOpts);

    // ============================================================
    // 六、价格变更日志
    // ============================================================

    /// <summary>分页查询价格变更日志（补全策略名、操作人账号、操作类型文案）</summary>
    public async Task<ApiResponse<PagedResult<PriceChangeLogDto>>> GetChangeLogsAsync(PriceChangeLogQueryDto q)
    {
        NormalizePaging(q);

        var query = _db.PriceChangeLogs.AsNoTracking().AsQueryable();

        if (q.StrategyId.HasValue)
        {
            query = query.Where(l => l.StrategyId == q.StrategyId.Value);
        }
        if (q.RuleId.HasValue)
        {
            query = query.Where(l => l.RuleId == q.RuleId.Value);
        }
        if (q.OperationType.HasValue)
        {
            query = query.Where(l => l.OperationType == q.OperationType.Value);
        }

        // 策略编号/名称模糊（EXISTS 子查询关联 price_strategy）
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(l =>
                _db.PriceStrategies.Any(s => s.Id == l.StrategyId
                                            && (s.StrategyNo.Contains(kw) || s.StrategyName.Contains(kw))));
        }

        // 操作人账号模糊（EXISTS 子查询关联 sys_user）
        if (!string.IsNullOrWhiteSpace(q.OperatorName))
        {
            var op = q.OperatorName.Trim();
            query = query.Where(l =>
                l.OperatorId.HasValue
                && _db.SysUsers.Any(u => u.Id == l.OperatorId.Value && u.UserName.Contains(op)));
        }

        if (q.StartTime.HasValue)
        {
            query = query.Where(l => l.OperateTime >= q.StartTime.Value);
        }
        if (q.EndTime.HasValue)
        {
            query = query.Where(l => l.OperateTime < q.EndTime.Value.AddDays(1));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        var strategyIds = items.Select(l => l.StrategyId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var strategyNameMap = strategyIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.PriceStrategies.AsNoTracking()
                .Where(s => strategyIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.StrategyName);

        var userMap = await GetUserNameMapAsync(
            items.Select(l => l.OperatorId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList());

        var dtos = items.Select(l =>
        {
            var d = _mapper.Map<PriceChangeLogDto>(l);
            d.OperationTypeText = PriceOperationTypeHelper.GetName(l.OperationType);
            d.StrategyName = l.StrategyId.HasValue && strategyNameMap.TryGetValue(l.StrategyId.Value, out var sn) ? sn : null;
            d.OperatorName = l.OperatorId.HasValue && userMap.TryGetValue(l.OperatorId.Value, out var un) ? un : null;
            return d;
        }).ToList();

        return ApiResponse<PagedResult<PriceChangeLogDto>>.Success(
            PagedResult<PriceChangeLogDto>.Create(dtos, total, q.Page, q.PageSize));
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    /// <summary>把规则更新写入实体，并逐字段记录变更日志</summary>
    private async Task ApplyRuleUpdateAsync(PriceRule rule, PriceRuleInputDto input, string? reason, DateTime now)
    {
        if (rule.ApplyScope != input.ApplyScope)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "apply_scope",
                ApplyScopeHelper.GetName(rule.ApplyScope), ApplyScopeHelper.GetName(input.ApplyScope), reason, now);
            rule.ApplyScope = input.ApplyScope;
        }
        if (rule.CategoryId != input.CategoryId)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "category_id",
                rule.CategoryId?.ToString(), input.CategoryId?.ToString(), reason, now);
            rule.CategoryId = input.CategoryId;
        }
        if (rule.CalcType != input.CalcType)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "calc_type",
                CalcTypeHelper.GetName(rule.CalcType), CalcTypeHelper.GetName(input.CalcType), reason, now);
            rule.CalcType = input.CalcType;
        }
        if (rule.CalcValue != decimal.Round(input.CalcValue, 4))
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "calc_value",
                rule.CalcValue.ToString("0.####"), decimal.Round(input.CalcValue, 4).ToString("0.####"), reason, now);
            rule.CalcValue = decimal.Round(input.CalcValue, 4);
        }
        var newMin = input.MinPrice.HasValue ? decimal.Round(input.MinPrice.Value, 2) : (decimal?)null;
        if (rule.MinPrice != newMin)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "min_price",
                rule.MinPrice?.ToString("0.00"), newMin?.ToString("0.00"), reason, now);
            rule.MinPrice = newMin;
        }
        if (rule.Status != input.Status)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "status",
                rule.Status == 1 ? "启用" : "停用", input.Status == 1 ? "启用" : "停用", reason, now);
            rule.Status = input.Status;
        }
        if (rule.MemMemberId != input.MemMemberId)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "mem_member_id",
                rule.MemMemberId?.ToString(), input.MemMemberId?.ToString(), reason, now);
            rule.MemMemberId = input.MemMemberId;
        }
        if (rule.CustomerLevelId != input.CustomerLevelId)
        {
            await AppendChangeLogAsync(rule.StrategyId, rule.Id, PriceOperationType.Update, "customer_level_id",
                rule.CustomerLevelId?.ToString(), input.CustomerLevelId?.ToString(), reason, now);
            rule.CustomerLevelId = input.CustomerLevelId;
        }
    }

    /// <summary>规则完整性校验（提交启用时调用），返回 null 表示通过</summary>
    private static string? ValidateRuleIntegrity(int strategyType, PriceRule rule, List<PriceRuleItem> items)
    {
        if (strategyType == (int)StrategyType.MemberExclusive && !rule.MemMemberId.HasValue)
        {
            return "客户专属价必须指定会员";
        }
        if (strategyType == (int)StrategyType.MemberLevel && !rule.CustomerLevelId.HasValue)
        {
            return "客户等级价必须指定客户等级";
        }
        if (rule.ApplyScope == (int)ApplyScope.Category && !rule.CategoryId.HasValue)
        {
            return "适用范围为指定分类时必须填写分类";
        }
        if (rule.ApplyScope == (int)ApplyScope.Material && items.Count == 0)
        {
            return "适用范围为指定商品时必须配置商品明细";
        }
        if (rule.CalcType == (int)CalcType.Discount && (rule.CalcValue <= 0 || rule.CalcValue > 1))
        {
            return "折扣率必须在 0~1 之间";
        }
        if (rule.CalcType == (int)CalcType.Fixed && rule.CalcValue <= 0)
        {
            return "固定价必须大于 0";
        }
        if (rule.CalcType == (int)CalcType.Reduce && rule.CalcValue < 0)
        {
            return "减免金额不能为负数";
        }
        if (strategyType == (int)StrategyType.Tiered && items.Count == 0)
        {
            return "批量阶梯价必须配置阶梯明细";
        }
        return null;
    }

    /// <summary>规则的简要描述（写变更日志用）</summary>
    private static string DescribeRule(PriceRule rule)
        => $"{ApplyScopeHelper.GetName(rule.ApplyScope)}/{CalcTypeHelper.GetName(rule.CalcType)}/{rule.CalcValue:0.####}/底价{rule.MinPrice?.ToString("0.00") ?? "-"}";

    /// <summary>写入价格变更日志（不落库，随调用方事务一起提交）</summary>
    private async Task AppendChangeLogAsync(
        long? strategyId, long? ruleId, PriceOperationType operationType,
        string fieldName, string? oldValue, string? newValue, string? reason, DateTime operateTime)
    {
        await _changeLogRepo.AddAsync(new PriceChangeLog
        {
            StrategyId = strategyId,
            RuleId = ruleId,
            OperationType = (int)operationType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            OperatorId = _currentUser.UserId,
            OperateTime = operateTime,
            Reason = reason
        });
    }

    /// <summary>规则实体 → DTO（批量补全会员名/等级名/分类名/商品名，避免 N+1）</summary>
    private async Task<List<PriceRuleDto>> MapRuleDtosAsync(List<PriceRule> rules, List<PriceRuleItem> items)
    {
        var memberIds = rules.Select(r => r.MemMemberId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var memberMap = memberIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.MemMembers.AsNoTracking()
                .Where(m => memberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name);

        var levelIds = rules.Select(r => r.CustomerLevelId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var levelMap = levelIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.MemMemberLevels.AsNoTracking()
                .Where(l => levelIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.LevelName);

        var categoryIds = rules.Select(r => r.CategoryId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var categoryMap = categoryIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.ProdCategories.AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CategoryName);

        var materialIds = items.Select(i => i.MaterialId).Distinct().ToList();
        var materialMap = materialIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.ProdSkus.AsNoTracking()
                .Where(s => materialIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SkuName ?? string.Empty);

        return rules.Select(r =>
        {
            var d = _mapper.Map<PriceRuleDto>(r);
            d.ApplyScopeText = ApplyScopeHelper.GetName(r.ApplyScope);
            d.CalcTypeText = CalcTypeHelper.GetName(r.CalcType);
            d.MemMemberName = r.MemMemberId.HasValue && memberMap.TryGetValue(r.MemMemberId.Value, out var mn) ? mn : null;
            d.CustomerLevelName = r.CustomerLevelId.HasValue && levelMap.TryGetValue(r.CustomerLevelId.Value, out var ln) ? ln : null;
            d.CategoryName = r.CategoryId.HasValue && categoryMap.TryGetValue(r.CategoryId.Value, out var cn) ? cn : null;
            d.Items = items.Where(i => i.RuleId == r.Id).Select(i =>
            {
                var di = _mapper.Map<PriceRuleItemDto>(i);
                di.MaterialName = materialMap.TryGetValue(i.MaterialId, out var mm) ? mm : null;
                return di;
            }).ToList();
            return d;
        }).OrderBy(r => r.Id).ToList();
    }

    /// <summary>批量查 sys_user 账号（列表展示创建人/操作人）</summary>
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

    /// <summary>分页参数归一化（与项目其他模块口径一致：Page≥1，1≤PageSize≤100）</summary>
    private static void NormalizePaging(IPagedQuery q)
    {
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1 || q.PageSize > 100) q.PageSize = 10;
    }
}
