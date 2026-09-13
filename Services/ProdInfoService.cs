using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 商品（SPU）服务实现。
/// 核心：新增/修改商品在【同一事务】中聚合保存 4 部分数据：
///   1. SPU 基础信息（prod_info）
///   2. 规格项 + 规格值（prod_spec_item / prod_spec_value）
///   3. SKU 列表（prod_sku，价格/库存/编码全部绑定在 SKU 上）
///   4. 图片（prod_image，主图/轮播图/详情图）
/// 修改时规格/值/SKU/图片采用"全量替换"策略（先删后插）。
///
/// 本版本的两个关键约定（2026-09 改造）：
///   A. prod_sku.sku_code 由后端 GenerateSkuCode() 自动生成（SKU + yyyyMMddHHmmss + 4位随机数），
///      前端传值一律忽略，数据库有 uk_sku_code 唯一索引兜底。
///   B. prod_sku.spec_values 存"规格值ID组合"（如 "10" 或 "10,15"），
///      由后端从前端提交的规格数组中提取 spec_value_id 拼接而成。
/// </summary>
public class ProdInfoService : IProdInfoService
{
    /// <summary>
    /// 规格值组合文本的分隔符（兼容旧前端 "1.60/防蓝光膜"、"1.60,防蓝光膜" 等写法）
    /// 注意：不要把空格放进分隔符，规格值本身可能含空格。
    /// </summary>
    private static readonly char[] SpecValueSeparators = { ',', '/', '|', '+' };

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public ProdInfoService(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // ============================================================
    // 查询
    // ============================================================

    /// <summary>
    /// 分页查询商品列表。
    /// keyword 同时匹配商品名称(prod_info_name)和商品编码(prod_info_code)；
    /// 价格区间/库存总量由 SKU 表聚合（价格库存不在 SPU 上）。
    /// </summary>
    public async Task<ApiResponse<PagedResult<ProdInfoDto>>> GetPagedListAsync(
        string? keyword, long? categoryId, long? supplierId, int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _db.ProdInfos
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 名称或编码模糊匹配
            query = query.Where(p =>
                p.ProdInfoName.Contains(keyword) ||
                (p.ProdInfoCode != null && p.ProdInfoCode.Contains(keyword)));
        }
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.ProdCategoryId == categoryId.Value);
        }
        if (supplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = _mapper.Map<List<ProdInfoDto>>(items);

        // 聚合本页商品的 SKU 价格区间与库存总量（一次查询，避免 N+1）
        var productIds = list.Select(p => p.Id).ToList();
        var skuAgg = await _db.ProdSkus
            .Where(s => productIds.Contains(s.ProdInfoId) && s.Status == 1)
            .GroupBy(s => s.ProdInfoId)
            .Select(g => new
            {
                ProdInfoId = g.Key,
                MinPrice = g.Min(x => x.RetailPrice),
                MaxPrice = g.Max(x => x.RetailPrice),
                TotalStock = g.Sum(x => x.Stock)
            })
            .ToListAsync();

        foreach (var dto in list)
        {
            var agg = skuAgg.FirstOrDefault(a => a.ProdInfoId == dto.Id);
            if (agg != null)
            {
                dto.MinPrice = agg.MinPrice;
                dto.MaxPrice = agg.MaxPrice;
                dto.TotalStock = agg.TotalStock;
            }
        }

        return ApiResponse<PagedResult<ProdInfoDto>>.Success(
            PagedResult<ProdInfoDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 商品详情：一次性返回 SPU + 图片 + 全部规格项(含规格值) + 全部 SKU。
    /// </summary>
    public async Task<ApiResponse<ProdInfoDetailDto>> GetDetailAsync(long id)
    {
        var product = await _db.ProdInfos
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProdInfoDetailDto>.Fail("商品不存在", 404);
        }

        var dto = _mapper.Map<ProdInfoDetailDto>(product);

        // 图片（按 sort 排序）
        dto.Images = await _db.ProdImages
            .Where(i => i.ProdInfoId == id)
            .OrderBy(i => i.Sort)
            .Select(i => new ProdImageDto
            {
                Id = i.Id,
                ProdInfoId = i.ProdInfoId,
                ImageType = i.ImageType,
                ImageUrl = i.ImageUrl,
                Sort = i.Sort
            })
            .ToListAsync();

        // 规格项（按 sort 排序）
        var specItems = await _db.ProdSpecItems
            .Where(i => i.ProdInfoId == id)
            .OrderBy(i => i.Sort)
            .AsNoTracking()
            .ToListAsync();

        // 规格值（冗余列 prod_info_id 可直接按商品查，再按 spec_id 分组）
        var specValues = await _db.ProdSpecValues
            .Where(v => v.ProdInfoId == id)
            .OrderBy(v => v.Sort)
            .AsNoTracking()
            .ToListAsync();

        dto.SpecItems = specItems.Select(item => new SpecItemDto
        {
            Id = item.Id,
            ProdInfoId = item.ProdInfoId,
            SpecName = item.SpecName,
            SpecType = item.SpecType,
            IsRequired = item.IsRequired,
            Sort = item.Sort,
            Values = specValues
                .Where(v => v.SpecId == item.Id)
                .Select(v => new SpecValueDto
                {
                    Id = v.Id,
                    SpecId = v.SpecId,
                    ProdInfoId = v.ProdInfoId,
                    SpecValue = v.SpecValue,
                    Sort = v.Sort
                })
                .ToList()
        }).ToList();

        // SKU（价格/库存/编码）
        dto.Skus = await _db.ProdSkus
            .Where(s => s.ProdInfoId == id)
            .OrderBy(s => s.Id)
            .AsNoTracking()
            .Select(s => new ProdSkuDto
            {
                Id = s.Id,
                ProdInfoId = s.ProdInfoId,
                SkuCode = s.SkuCode,
                SkuName = s.SkuName,
                SpecValues = s.SpecValues,
                RetailPrice = s.RetailPrice,
                SalePrice = s.SalePrice,
                Stock = s.Stock,
                SalesCount = s.SalesCount,
                Image = s.Image,
                Status = s.Status,
                CreateTime = s.CreateTime
            })
            .ToListAsync();

        // spec_values 现在存的是ID组合（"10,15"），这里反查成展示文本与规格明细，方便前端直接显示
        await FillSpecValueTextAsync(dto.Skus);

        return ApiResponse<ProdInfoDetailDto>.Success(dto);
    }

    // ============================================================
    // 新增 / 修改（事务聚合保存）
    // ============================================================

    /// <summary>
    /// 新增商品：事务内依次保存 SPU → 规格 → SKU → 图片。
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(ProdInfoCreateDto dto)
    {
        // 1. 关联校验
        var relationError = await ValidateRelationsAsync(dto.ProdCategoryId, dto.SupplierId);
        if (relationError != null)
        {
            return ApiResponse<object>.Fail(relationError);
        }

        // 2. 同一商品下不允许出现规格组合重复的 SKU
        var specError = ValidateSkuSpecCombination(dto.Skus);
        if (specError != null)
        {
            return ApiResponse<object>.Fail(specError);
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 2.1 保存 SPU
            var product = _mapper.Map<ProdInfo>(dto);
            SyncMainImage(product, dto.Images);
            _db.ProdInfos.Add(product);
            await _db.SaveChangesAsync();  // 拿到 product.Id

            // 2.2 保存规格项 + 规格值，并拿到"规格值 → ID"索引（SKU 要用它拼 spec_values）
            var specIndex = await SaveSpecItemsAsync(product.Id, dto.SpecItems);

            // 2.3 保存 SKU（sku_code 后端生成、spec_values 存规格值ID组合）
            await SaveSkusAsync(product.Id, dto.Skus, specIndex, product.ProdInfoName);

            // 2.4 保存图片
            SaveImages(product.Id, dto.Images);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return ApiResponse<object>.Success(new { id = product.Id }, "商品创建成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 修改商品：事务内更新 SPU，并全量替换规格/SKU/图片。
    /// 注意：SKU 全量重建后 sku_code 会重新生成（编码由后端维护，业务上不依赖历史编码）。
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(ProdInfoUpdateDto dto)
    {
        var product = await _db.ProdInfos.FirstOrDefaultAsync(p => p.Id == dto.Id);
        if (product == null)
        {
            return ApiResponse<object>.Fail("商品不存在", 404);
        }

        var relationError = await ValidateRelationsAsync(dto.ProdCategoryId, dto.SupplierId);
        if (relationError != null)
        {
            return ApiResponse<object>.Fail(relationError);
        }

        var specError = ValidateSkuSpecCombination(dto.Skus);
        if (specError != null)
        {
            return ApiResponse<object>.Fail(specError);
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 更新 SPU 基础字段（AutoMapper 覆盖；Id/销量/时间不被覆盖）
            _mapper.Map(dto, product);
            SyncMainImage(product, dto.Images);

            // 2. 全量替换规格值/规格项（数据库无物理外键，手工删除）
            var oldSpecItemIds = await _db.ProdSpecItems
                .Where(i => i.ProdInfoId == dto.Id)
                .Select(i => i.Id)
                .ToListAsync();

            if (oldSpecItemIds.Count > 0)
            {
                var oldValues = _db.ProdSpecValues.Where(v => oldSpecItemIds.Contains(v.SpecId));
                _db.ProdSpecValues.RemoveRange(oldValues);
            }
            _db.ProdSpecItems.RemoveRange(_db.ProdSpecItems.Where(i => i.ProdInfoId == dto.Id));

            // 3. 全量替换 SKU
            _db.ProdSkus.RemoveRange(_db.ProdSkus.Where(s => s.ProdInfoId == dto.Id));

            // 4. 全量替换图片
            _db.ProdImages.RemoveRange(_db.ProdImages.Where(i => i.ProdInfoId == dto.Id));

            await _db.SaveChangesAsync();  // 先落库删除，避免唯一约束冲突

            // 5. 重新写入规格（规格值ID会变，必须重新生成索引给 SKU 用）→ SKU → 图片
            var specIndex = await SaveSpecItemsAsync(dto.Id, dto.SpecItems);
            await SaveSkusAsync(dto.Id, dto.Skus, specIndex, product.ProdInfoName);
            SaveImages(dto.Id, dto.Images);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id = dto.Id }, "商品修改成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 删除商品：事务内级联清理图片/SKU/规格值/规格项（数据库未建物理外键，需手工删）。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var product = await _db.ProdInfos.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            return ApiResponse<object>.Fail("商品不存在", 404);
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.ProdImages.RemoveRange(_db.ProdImages.Where(i => i.ProdInfoId == id));
            _db.ProdSkus.RemoveRange(_db.ProdSkus.Where(s => s.ProdInfoId == id));
            _db.ProdSpecValues.RemoveRange(_db.ProdSpecValues.Where(v => v.ProdInfoId == id));
            _db.ProdSpecItems.RemoveRange(_db.ProdSpecItems.Where(i => i.ProdInfoId == id));
            _db.ProdInfos.Remove(product);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse<object>.Success(new { id }, "删除成功");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 快速上下架（列表 Switch 开关调用，只改状态）。
    /// </summary>
    public async Task<ApiResponse<object>> SetStatusAsync(long id, int status)
    {
        var product = await _db.ProdInfos.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            return ApiResponse<object>.Fail("商品不存在", 404);
        }

        product.Status = status == 1 ? 1 : 0;
        await _db.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id }, "状态更新成功");
    }

    // ============================================================
    // 私有辅助方法 —— 校验
    // ============================================================

    /// <summary>校验分类必存在；供应商可空但传了必须存在</summary>
    private async Task<string?> ValidateRelationsAsync(long prodCategoryId, long? supplierId)
    {
        if (!await _db.ProdCategories.AnyAsync(c => c.Id == prodCategoryId))
        {
            return "所选商品分类不存在";
        }
        if (supplierId.HasValue && !await _db.SupSuppliers.AnyAsync(s => s.Id == supplierId.Value))
        {
            return "所选供应商不存在";
        }
        return null;
    }

    /// <summary>
    /// 规格组合唯一性校验：同一商品下不允许两个 SKU 选同一组规格值
    /// （替代原来的 SKU 编码唯一性校验 —— 编码现在由后端生成，不可能重复）。
    /// 比较时用"排序后的规格值ID/文本"作键，避免顺序差异导致漏判。
    /// </summary>
    private static string? ValidateSkuSpecCombination(List<ProdSkuCreateDto> skus)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dto in skus)
        {
            string? key = null;

            // 优先用规格对象数组里的ID
            if (dto.Specs is { Count: > 0 })
            {
                var ids = dto.Specs
                    .Where(s => s.SpecValueId > 0)
                    .Select(s => s.SpecValueId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                if (ids.Count > 0)
                {
                    key = string.Join(",", ids);
                }
            }

            // 退回用 specValues 文本/ID串
            key ??= NormalizeSpecTokens(dto.SpecValues);

            if (string.IsNullOrEmpty(key)) continue;  // 未传规格的 SKU 不参与去重

            if (!seen.Add(key))
            {
                return $"提交的 SKU 列表中存在规格组合重复的 SKU（{key}），请检查";
            }
        }

        return null;
    }

    // ============================================================
    // 私有辅助方法 —— SKU 编码生成
    // ============================================================

    /// <summary>
    /// 生成 SKU 编码：固定前缀 SKU + 时间戳 yyyyMMddHHmmss + 4位随机数。
    /// 例：SKU202609082310451234
    /// （统一委托给 SkuCodeGenerator，保证全系统规则一致、只维护一份）
    /// </summary>
    private static string GenerateSkuCode() => SkuCodeGenerator.Generate();

    /// <summary>
    /// 生成全局唯一的 SKU 编码：批次内去重 + 数据库去重，冲突自动重试。
    /// </summary>
    /// <param name="batchCodes">本批次已生成的编码集合（一次保存多个 SKU 时用）</param>
    private async Task<string> GenerateUniqueSkuCodeAsync(HashSet<string> batchCodes)
    {
        return await SkuCodeGenerator.GenerateUniqueAsync(
            existsAsync: code => _db.ProdSkus.AnyAsync(s => s.SkuCode == code),
            batchCodes: batchCodes);
    }

    // ============================================================
    // 私有辅助方法 —— 保存规格 / SKU / 图片
    // ============================================================

    /// <summary>
    /// 保存规格项及其规格值（新增商品时调用；修改时先删后插也走这里）。
    /// </summary>
    /// <returns>
    /// 规格值索引：保存后每个规格值都拿到了数据库自增ID，
    /// 用它可以把 SKU 提交的"规格值"换算成"规格值ID组合"。
    /// </returns>
    private async Task<SpecValueIndex> SaveSpecItemsAsync(long productId, List<SpecItemDto> specItems)
    {
        var index = new SpecValueIndex();

        for (var i = 0; i < specItems.Count; i++)
        {
            var itemDto = specItems[i];
            if (string.IsNullOrWhiteSpace(itemDto.SpecName)) continue;

            var specName = itemDto.SpecName.Trim();

            var item = new ProdSpecItem
            {
                ProdInfoId = productId,
                SpecName = specName,
                SpecType = itemDto.SpecType == 2 ? 2 : 1,
                IsRequired = itemDto.IsRequired == 1 ? 1 : 0,
                Sort = itemDto.Sort ?? (i + 1)
            };
            _db.ProdSpecItems.Add(item);
            await _db.SaveChangesAsync();  // 拿到规格项 Id

            if (itemDto.Values.Count == 0) continue;

            // 先把规格值实体加入上下文，SaveChanges 后统一回填 Id（比逐个 SaveChanges 少几次往返）
            var pending = new List<ProdSpecValue>();
            for (var j = 0; j < itemDto.Values.Count; j++)
            {
                var valueDto = itemDto.Values[j];
                if (string.IsNullOrWhiteSpace(valueDto.SpecValue)) continue;

                var valueText = valueDto.SpecValue.Trim();
                var value = new ProdSpecValue
                {
                    SpecId = item.Id,
                    ProdInfoId = productId,  // 冗余列同步填充
                    SpecValue = valueText,
                    Sort = valueDto.Sort ?? (j + 1)
                };
                _db.ProdSpecValues.Add(value);
                pending.Add(value);
            }

            await _db.SaveChangesAsync();  // 拿到全部规格值 Id

            // 登记到索引：规格项名 + 规格值文本 → 规格值ID
            foreach (var value in pending)
            {
                index.Add(specName, value.SpecValue, value.Id);
            }
        }

        return index;
    }

    /// <summary>
    /// 保存 SKU 列表（核心改造点）。
    /// 1) sku_code 由后端 GenerateUniqueSkuCodeAsync() 自动生成，完全不接收前端传值；
    /// 2) spec_values 存"规格值ID组合"（如 "10,15"），由 BuildSpecValueIds() 从规格配置中提取；
    /// 3) sku_name 留空时用"商品名 + 规格值文本"兜底。
    /// </summary>
    private async Task SaveSkusAsync(
        long productId,
        List<ProdSkuCreateDto> skus,
        SpecValueIndex specIndex,
        string? productName)
    {
        // 本批次已生成的编码，防止同一秒内批量新增撞号
        var batchCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dto in skus)
        {
            // ① 提取规格值ID并拼成 "10,15"
            var specValueIds = BuildSpecValueIds(dto, specIndex);

            // ② SKU 编码：后端生成（dto 里已没有 SkuCode 属性，前端传不进来）
            var skuCode = await GenerateUniqueSkuCodeAsync(batchCodes);

            // ③ SKU 名称兜底：未填时用"商品名 规格值文本"
            var skuName = dto.SkuName?.Trim();
            if (string.IsNullOrWhiteSpace(skuName))
            {
                skuName = BuildDefaultSkuName(productName, specIndex, specValueIds);
            }

            _db.ProdSkus.Add(new ProdSku
            {
                ProdInfoId = productId,
                SkuCode = skuCode,
                SkuName = skuName,
                SpecValues = specValueIds,
                RetailPrice = dto.RetailPrice,
                SalePrice = dto.SalePrice,
                Stock = dto.Stock,
                Image = dto.Image,
                Status = dto.Status == 0 ? 0 : 1
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>保存图片（内存操作，统一 SaveChanges 由调用方执行）</summary>
    private void SaveImages(long productId, List<ProdImageSaveDto> images)
    {
        for (var i = 0; i < images.Count; i++)
        {
            var dto = images[i];
            if (string.IsNullOrWhiteSpace(dto.ImageUrl)) continue;

            _db.ProdImages.Add(new ProdImage
            {
                ProdInfoId = productId,
                ImageType = dto.ImageType,
                ImageUrl = dto.ImageUrl.Trim(),
                Sort = dto.Sort ?? i
            });
        }
    }

    // ============================================================
    // 私有辅助方法 —— 规格值ID提取（本次改造的核心算法）
    // ============================================================

    /// <summary>
    /// 从 SKU 提交数据中提取"规格值ID"，拼接成 "10" 或 "10,15" 形式的字符串，
    /// 最终写入 prod_sku.spec_values。
    ///
    /// 取值优先级（逐级兜底，保证新旧前端都能跑通）：
    ///   ① dto.Specs 对象数组里的 specValueId —— 最准，直接用；
    ///   ② dto.SpecValues 已经是纯ID串（"10,15"）—— 规范化后直接用；
    ///   ③ dto.SpecValues 是展示文本（"1.60/防蓝光膜"）—— 用规格索引反查成ID（兼容旧前端）。
    /// 三种都取不到时返回空串（spec_values 允许为空）。
    /// </summary>
    /// <param name="dto">前端提交的单个 SKU</param>
    /// <param name="index">本次保存规格后生成的"规格值 → ID"索引（独立新增 SKU 场景可为 null）</param>
    private static string BuildSpecValueIds(ProdSkuCreateDto dto, SpecValueIndex? index)
    {
        // ---------- ① 规格对象数组：[{ specId, specValueId, specValue }] ----------
        if (dto.Specs is { Count: > 0 })
        {
            var fromSpecs = dto.Specs
                .Where(s => s.SpecValueId > 0)
                .Select(s => s.SpecValueId)
                .Distinct()
                .ToList();

            if (fromSpecs.Count > 0)
            {
                // ID 必须落在本次规格值范围内。
                // 修改商品时规格是先删后插的，旧的 spec_value_id 会失效，此时不能直接信任。
                if (index is null || fromSpecs.All(index.Contains))
                {
                    return string.Join(",", fromSpecs);
                }
            }

            // ID 失效 → 退一步用规格值文本反查
            if (index is not null)
            {
                var byText = new List<long>();
                foreach (var spec in dto.Specs)
                {
                    if (spec.SpecValueId > 0 && index.Contains(spec.SpecValueId))
                    {
                        byText.Add(spec.SpecValueId);
                    }
                    else if (!string.IsNullOrWhiteSpace(spec.SpecValue)
                             && index.TryGetByValueText(spec.SpecValue, out var textId))
                    {
                        byText.Add(textId);
                    }
                }
                if (byText.Count > 0)
                {
                    return string.Join(",", byText.Distinct());
                }
            }
        }

        var raw = dto.SpecValues?.Trim();
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        // 统一按分隔符切开（"10,15" 和 "1.60/防蓝光膜" 都能切）
        var tokens = raw.Split(SpecValueSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return string.Empty;

        // ---------- ② 纯ID串："10,15" ----------
        if (tokens.All(t => long.TryParse(t, out _)))
        {
            var ids = tokens.Select(long.Parse).Where(id => id > 0).Distinct().ToList();
            if (ids.Count > 0 && (index is null || ids.All(index.Contains)))
            {
                return string.Join(",", ids);
            }
        }

        // ---------- ③ 展示文本："1.60/防蓝光膜" 反查成ID ----------
        if (index is not null)
        {
            var resolved = new List<long>();
            foreach (var token in tokens)
            {
                // 支持 "规格名:规格值" 写法，也支持直接写规格值
                var specName = (string?)null;
                var valueText = token;
                var colon = token.IndexOf(':');
                if (colon > 0)
                {
                    specName = token[..colon].Trim();
                    valueText = token[(colon + 1)..].Trim();
                }

                long id;
                var ok = specName != null
                    ? index.TryGetId(specName, valueText, out id)
                    : index.TryGetByValueText(valueText, out id);

                if (ok) resolved.Add(id);
            }
            if (resolved.Count > 0)
            {
                return string.Join(",", resolved.Distinct());
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 兜底生成 SKU 名称：商品名 + 规格值展示文本（如"防蓝光镜片 1.60/防蓝光膜"）。
    /// </summary>
    private static string? BuildDefaultSkuName(string? productName, SpecValueIndex index, string specValueIds)
    {
        var specText = index.ToDisplayText(specValueIds);  // "1.60/防蓝光膜"
        if (string.IsNullOrEmpty(specText)) return productName;
        return string.IsNullOrWhiteSpace(productName) ? specText : $"{productName} {specText}";
    }

    /// <summary>
    /// 把 "10,15" 这种ID组合还原成展示文本与规格明细（用于详情/列表回显，不落库）。
    /// </summary>
    private async Task FillSpecValueTextAsync(List<ProdSkuDto> skus)
    {
        // 1. 收集所有 SKU 用到的规格值ID
        var allIds = new HashSet<long>();
        foreach (var sku in skus)
        {
            foreach (var id in ParseSpecValueIds(sku.SpecValues))
            {
                allIds.Add(id);
            }
        }
        if (allIds.Count == 0) return;

        // 2. 一次查询拿到 ID → 规格值文本 / 规格项ID
        var dict = await _db.ProdSpecValues
            .AsNoTracking()
            .Where(v => allIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v);

        // 3. 回填展示文本与规格明细
        foreach (var sku in skus)
        {
            var ids = ParseSpecValueIds(sku.SpecValues);
            if (ids.Count == 0) continue;

            var texts = new List<string>();
            var specs = new List<SkuSpecValueDto>();

            foreach (var id in ids)
            {
                if (!dict.TryGetValue(id, out var value)) continue;
                texts.Add(value.SpecValue);
                specs.Add(new SkuSpecValueDto
                {
                    SpecId = value.SpecId,
                    SpecValueId = value.Id,
                    SpecValue = value.SpecValue
                });
            }

            sku.Specs = specs;
            sku.SpecValuesText = texts.Count > 0 ? string.Join("/", texts) : sku.SpecValues;
        }
    }

    /// <summary>把 "10,15" 解析成 [10, 15]；非法片段直接忽略</summary>
    private static List<long> ParseSpecValueIds(string? specValues)
    {
        var result = new List<long>();
        if (string.IsNullOrWhiteSpace(specValues)) return result;

        foreach (var token in specValues.Split(',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(token, out var id) && id > 0) result.Add(id);
        }
        return result;
    }

    /// <summary>把规格组合文本/ID串切成排序后的统一键（用于重复判断，忽略顺序与分隔符差异）</summary>
    private static string NormalizeSpecTokens(string? specValues)
    {
        if (string.IsNullOrWhiteSpace(specValues)) return string.Empty;

        var tokens = specValues.Split(SpecValueSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(",", tokens.OrderBy(t => t, StringComparer.Ordinal));
    }

    /// <summary>
    /// SPU 主图字段(main_image)与图片表(prod_image)保持同步：
    /// 未显式传 mainImage 时，取图片表中类型为主图('1')的第一张。
    /// </summary>
    private static void SyncMainImage(ProdInfo product, List<ProdImageSaveDto> images)
    {
        if (string.IsNullOrWhiteSpace(product.MainImage))
        {
            var main = images.FirstOrDefault(i => i.ImageType == "1");
            if (main != null)
            {
                product.MainImage = main.ImageUrl;
            }
        }
    }

    /// <summary>
    /// 规格值索引（保存规格项/规格值后构建）。
    /// 作用：把 SKU 提交的"规格值文本"或"规格名+规格值"换算成"规格值ID"，
    /// 同时提供 ID → 文本的反向查询，用于生成 SKU 名称与展示文本。
    /// 生命周期：仅存在于单次保存方法内。
    /// </summary>
    private sealed class SpecValueIndex
    {
        /// <summary>规格项名 →（规格值文本 → 规格值ID）</summary>
        private readonly Dictionary<string, Dictionary<string, long>> _bySpecName =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>规格值文本 → 规格值ID（跨规格项兜底匹配）</summary>
        private readonly Dictionary<string, long> _byValueText =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>规格值ID → 规格值文本（反向查询，生成展示文本用）</summary>
        private readonly Dictionary<long, string> _textById = new();

        /// <summary>本次生成的全部规格值ID（用于校验前端传来的ID是否合法）</summary>
        private readonly HashSet<long> _valueIds = new();

        public void Add(string specName, string valueText, long valueId)
        {
            if (!_bySpecName.TryGetValue(specName, out var map))
            {
                map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                _bySpecName[specName] = map;
            }
            map[valueText] = valueId;

            _byValueText.TryAdd(valueText, valueId);  // 同名规格值保留第一个（按规格项顺序）
            _textById[valueId] = valueText;
            _valueIds.Add(valueId);
        }

        /// <summary>该规格值ID是否是本次生成的（校验前端传值用）</summary>
        public bool Contains(long valueId) => _valueIds.Contains(valueId);

        /// <summary>按"规格项名 + 规格值文本"查ID，查不到再按值文本兜底</summary>
        public bool TryGetId(string specName, string valueText, out long valueId)
        {
            if (_bySpecName.TryGetValue(specName, out var map) && map.TryGetValue(valueText, out valueId))
            {
                return true;
            }
            return _byValueText.TryGetValue(valueText, out valueId);
        }

        /// <summary>仅按规格值文本查ID（旧前端只传 "1.60/防蓝光膜" 时用）</summary>
        public bool TryGetByValueText(string valueText, out long valueId)
            => _byValueText.TryGetValue(valueText, out valueId);

        /// <summary>把 "10,15" 还原成 "1.60/防蓝光膜"</summary>
        public string ToDisplayText(string specValueIds, string separator = "/")
        {
            if (string.IsNullOrWhiteSpace(specValueIds)) return string.Empty;

            var texts = new List<string>();
            foreach (var token in specValueIds.Split(',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(token, out var id) && _textById.TryGetValue(id, out var text))
                {
                    texts.Add(text);
                }
            }
            return string.Join(separator, texts);
        }
    }
}
