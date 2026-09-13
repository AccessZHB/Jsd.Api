using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 商品 SKU 服务实现（独立新增/修改单条 SKU；随 SPU 一起保存走 ProdInfoService）。
/// 本版本约定（2026-09 改造）：
///   A. sku_code 由后端 SkuCodeGenerator 自动生成（SKU + yyyyMMddHHmmss + 4位随机数），前端传值忽略；
///   B. spec_values 存"规格值ID组合"（如 "10,15"），且所有ID必须属于该 SKU 所属商品。
/// </summary>
public class ProdSkuService : IProdSkuService
{
    private readonly IRepository<ProdSku> _skuRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IRepository<ProdSpecValue> _specValueRepository;
    private readonly IMapper _mapper;

    public ProdSkuService(
        IRepository<ProdSku> skuRepository,
        IRepository<ProdInfo> productRepository,
        IRepository<ProdSpecValue> specValueRepository,
        IMapper mapper)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
        _specValueRepository = specValueRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 根据商品ID获取所有SKU（按 Id 升序）
    /// </summary>
    public async Task<ApiResponse<List<ProdSkuDto>>> GetListByProductAsync(long prodInfoId)
    {
        var skus = await _skuRepository.Query()
            .AsNoTracking()
            .Where(s => s.ProdInfoId == prodInfoId)
            .OrderBy(s => s.Id)
            .ToListAsync();

        var list = _mapper.Map<List<ProdSkuDto>>(skus);

        // spec_values 存的是ID组合，这里反查成展示文本，前端可直接显示
        await FillSpecValueTextAsync(list);

        return ApiResponse<List<ProdSkuDto>>.Success(list);
    }

    /// <summary>
    /// 根据 ID 获取SKU详情
    /// </summary>
    public async Task<ApiResponse<ProdSkuDto>> GetByIdAsync(long id)
    {
        var sku = await _skuRepository.GetByIdAsync(id);
        if (sku == null)
        {
            return ApiResponse<ProdSkuDto>.Fail("SKU不存在", 404);
        }

        var dto = _mapper.Map<ProdSkuDto>(sku);
        await FillSpecValueTextAsync(new List<ProdSkuDto> { dto });

        return ApiResponse<ProdSkuDto>.Success(dto);
    }

    /// <summary>
    /// 新增单个SKU（sku_code 由后端生成，前端传值忽略）
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(ProdSkuCreateDto dto)
    {
        // 1. 商品必须存在（随 SPU 保存的场景由聚合逻辑处理，此处为独立新增）
        if (!dto.ProdInfoId.HasValue ||
            !await _productRepository.AnyAsync(p => p.Id == dto.ProdInfoId.Value))
        {
            return ApiResponse<object>.Fail("商品不存在");
        }

        var productId = dto.ProdInfoId.Value;

        // 2. 提取规格值ID（"10,15"），并校验这些ID确实属于该商品（数据库无物理外键，业务层强校验）
        var specValueIds = ExtractSpecValueIds(dto);
        var validError = await ValidateSpecValueIdsAsync(specValueIds, productId);
        if (validError != null)
        {
            return ApiResponse<object>.Fail(validError);
        }

        // 3. DTO → Entity（AutoMapper 已忽略 SkuCode / SpecValues，下面手工赋值）
        var sku = _mapper.Map<ProdSku>(dto);
        sku.ProdInfoId = productId;
        sku.SkuCode = await SkuCodeGenerator.GenerateUniqueAsync(
            existsAsync: code => _skuRepository.AnyAsync(s => s.SkuCode == code));
        sku.SpecValues = string.Join(",", specValueIds);

        // 4. SKU 名称兜底：商品名 + 规格值文本
        if (string.IsNullOrWhiteSpace(sku.SkuName))
        {
            sku.SkuName = await BuildDefaultSkuNameAsync(productId, specValueIds);
        }

        await _skuRepository.AddAsync(sku);
        await _skuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = sku.Id, skuCode = sku.SkuCode }, "SKU创建成功");
    }

    /// <summary>
    /// 修改SKU（价格/库存/规格组合）。sku_code 由后端生成，不允许前端修改。
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(ProdSkuUpdateDto dto)
    {
        // 1. SKU必须存在
        var sku = await _skuRepository.GetByIdAsync(dto.Id);
        if (sku == null)
        {
            return ApiResponse<object>.Fail("SKU不存在", 404);
        }

        // 2. 若传了规格组合，先提取ID并校验归属
        if (dto.Specs is { Count: > 0 } || !string.IsNullOrWhiteSpace(dto.SpecValues))
        {
            var specValueIds = ExtractSpecValueIds(new ProdSkuCreateDto
            {
                Specs = dto.Specs,
                SpecValues = dto.SpecValues
            });

            var validError = await ValidateSpecValueIdsAsync(specValueIds, sku.ProdInfoId);
            if (validError != null)
            {
                return ApiResponse<object>.Fail(validError);
            }

            sku.SpecValues = string.Join(",", specValueIds);
        }

        // 3. DTO 增量覆盖并保存
        //    （ProdInfoId / SkuCode / SpecValues 在映射中被 Ignore：商品归属与编码不可改，规格上面已单独处理）
        _mapper.Map(dto, sku);
        await _skuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = sku.Id }, "SKU修改成功");
    }

    /// <summary>
    /// 删除SKU
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var sku = await _skuRepository.GetByIdAsync(id);
        if (sku == null)
        {
            return ApiResponse<object>.Fail("SKU不存在", 404);
        }

        _skuRepository.Remove(sku);
        await _skuRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id }, "删除成功");
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    /// <summary>
    /// 从 SKU 提交数据中提取规格值ID列表。
    /// 优先取 Specs 对象数组里的 specValueId，其次取 SpecValues 里的纯ID串（"10,15"）。
    /// 独立新增场景拿不到"规格值文本 → ID"索引（商品规格已存在时可按需扩展），
    /// 所以这里只认ID，不接受展示文本。
    /// </summary>
    private static List<long> ExtractSpecValueIds(ProdSkuCreateDto dto)
    {
        if (dto.Specs is { Count: > 0 })
        {
            var fromSpecs = dto.Specs
                .Where(s => s.SpecValueId > 0)
                .Select(s => s.SpecValueId)
                .Distinct()
                .ToList();
            if (fromSpecs.Count > 0) return fromSpecs;
        }

        if (string.IsNullOrWhiteSpace(dto.SpecValues)) return new List<long>();

        var result = new List<long>();
        foreach (var token in dto.SpecValues.Split(',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(token, out var id) && id > 0) result.Add(id);
        }
        return result.Distinct().ToList();
    }

    /// <summary>校验规格值ID是否真实存在且属于指定商品</summary>
    private async Task<string?> ValidateSpecValueIdsAsync(List<long> specValueIds, long productId)
    {
        if (specValueIds.Count == 0) return null;  // 允许不传规格

        var validCount = await _specValueRepository.Query()
            .AsNoTracking()
            .CountAsync(v => v.ProdInfoId == productId && specValueIds.Contains(v.Id));

        return validCount == specValueIds.Count
            ? null
            : "存在无效或不属于该商品的规格值ID，请检查";
    }

    /// <summary>兜底生成 SKU 名称：商品名 + 规格值文本</summary>
    private async Task<string?> BuildDefaultSkuNameAsync(long productId, List<long> specValueIds)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        var productName = product?.ProdInfoName;

        if (specValueIds.Count == 0) return productName;

        var texts = await _specValueRepository.Query()
            .AsNoTracking()
            .Where(v => specValueIds.Contains(v.Id))
            .Select(v => v.SpecValue)
            .ToListAsync();

        if (texts.Count == 0) return productName;

        var specText = string.Join("/", texts);
        return string.IsNullOrWhiteSpace(productName) ? specText : $"{productName} {specText}";
    }

    /// <summary>把 "10,15" 反查成展示文本与规格明细（仅用于响应，不落库）</summary>
    private async Task FillSpecValueTextAsync(List<ProdSkuDto> skus)
    {
        var allIds = new HashSet<long>();
        foreach (var sku in skus)
        {
            if (string.IsNullOrWhiteSpace(sku.SpecValues)) continue;

            foreach (var token in sku.SpecValues.Split(',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(token, out var id) && id > 0) allIds.Add(id);
            }
        }
        if (allIds.Count == 0) return;

        var values = await _specValueRepository.Query()
            .AsNoTracking()
            .Where(v => allIds.Contains(v.Id))
            .ToListAsync();
        var dict = values.ToDictionary(v => v.Id, v => v);

        foreach (var sku in skus)
        {
            if (string.IsNullOrWhiteSpace(sku.SpecValues)) continue;

            var texts = new List<string>();
            var specs = new List<SkuSpecValueDto>();

            foreach (var token in sku.SpecValues.Split(',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(token, out var id) || !dict.TryGetValue(id, out var value)) continue;
                texts.Add(value.SpecValue);
                specs.Add(new SkuSpecValueDto
                {
                    SpecId = value.SpecId,
                    SpecValueId = value.Id,
                    SpecValue = value.SpecValue
                });
            }

            if (texts.Count == 0) continue;
            sku.Specs = specs;
            sku.SpecValuesText = string.Join("/", texts);
        }
    }
}
