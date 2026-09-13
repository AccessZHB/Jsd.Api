using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 商品规格服务实现（规格项 + 规格值）
/// 外键：prod_spec_item.prod_info_id → prod_info.id；
///       prod_spec_value.spec_id → prod_spec_item.id（另有 prod_info_id 冗余列）。
/// </summary>
public class ProdSpecService : IProdSpecService
{
    private readonly IRepository<ProdSpecItem> _specItemRepository;
    private readonly IRepository<ProdSpecValue> _specValueRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IMapper _mapper;

    public ProdSpecService(
        IRepository<ProdSpecItem> specItemRepository,
        IRepository<ProdSpecValue> specValueRepository,
        IRepository<ProdInfo> productRepository,
        IMapper mapper)
    {
        _specItemRepository = specItemRepository;
        _specValueRepository = specValueRepository;
        _productRepository = productRepository;
        _mapper = mapper;
    }

    // ============================================================
    // 规格项
    // ============================================================

    /// <summary>
    /// 根据商品ID获取所有规格项（按 sort 升序）
    /// </summary>
    public async Task<ApiResponse<List<SpecItemDto>>> GetItemsByProductAsync(long prodInfoId)
    {
        var items = await _specItemRepository.Query()
            .AsNoTracking()
            .Where(i => i.ProdInfoId == prodInfoId)
            .OrderBy(i => i.Sort)
            .ThenBy(i => i.Id)
            .ToListAsync();

        return ApiResponse<List<SpecItemDto>>.Success(_mapper.Map<List<SpecItemDto>>(items));
    }

    /// <summary>
    /// 新增规格项（商品必须存在）
    /// </summary>
    public async Task<ApiResponse<object>> CreateItemAsync(SpecItemCreateDto dto)
    {
        // 商品必须存在
        if (!await _productRepository.AnyAsync(p => p.Id == dto.ProdInfoId))
        {
            return ApiResponse<object>.Fail("商品不存在");
        }

        var item = _mapper.Map<ProdSpecItem>(dto);
        await _specItemRepository.AddAsync(item);
        await _specItemRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = item.Id }, "规格项创建成功");
    }

    /// <summary>
    /// 修改规格项（改名/类型/排序）
    /// </summary>
    public async Task<ApiResponse<object>> UpdateItemAsync(SpecItemUpdateDto dto)
    {
        var item = await _specItemRepository.GetByIdAsync(dto.Id);
        if (item == null)
        {
            return ApiResponse<object>.Fail("规格项不存在", 404);
        }

        // DTO 增量覆盖（ProdInfoId 在映射中被 Ignore，商品归属不会被改动）
        _mapper.Map(dto, item);
        await _specItemRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = item.Id }, "规格项修改成功");
    }

    /// <summary>
    /// 删除规格项，并级联删除其下所有规格值。
    /// 注意：SKU 的 spec_values 是显示文本快照，不随规格删除联动。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteItemAsync(long id)
    {
        var item = await _specItemRepository.GetByIdAsync(id);
        if (item == null)
        {
            return ApiResponse<object>.Fail("规格项不存在", 404);
        }

        // 1. 先删该规格项下的所有规格值（spec_id 关联）
        var values = await _specValueRepository.GetListAsync(v => v.SpecId == id);
        foreach (var value in values)
        {
            _specValueRepository.Remove(value);
        }

        // 2. 再删规格项；同一个 SaveChanges 保证原子性
        _specItemRepository.Remove(item);
        await _specItemRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id, removedValues = values.Count }, "删除成功");
    }

    // ============================================================
    // 规格值
    // ============================================================

    /// <summary>
    /// 根据规格项ID获取所有规格值（按 sort 升序）
    /// </summary>
    public async Task<ApiResponse<List<SpecValueDto>>> GetValuesByItemAsync(long specId)
    {
        var values = await _specValueRepository.Query()
            .AsNoTracking()
            .Where(v => v.SpecId == specId)
            .OrderBy(v => v.Sort)
            .ThenBy(v => v.Id)
            .ToListAsync();

        return ApiResponse<List<SpecValueDto>>.Success(_mapper.Map<List<SpecValueDto>>(values));
    }

    /// <summary>
    /// 新增规格值（规格项必须存在）
    /// </summary>
    public async Task<ApiResponse<object>> CreateValueAsync(SpecValueCreateDto dto)
    {
        // 规格项必须存在
        var item = await _specItemRepository.GetByIdAsync(dto.SpecId);
        if (item == null)
        {
            return ApiResponse<object>.Fail("规格项不存在");
        }

        var value = _mapper.Map<ProdSpecValue>(dto);

        // 冗余列 prod_info_id 未传时，从规格项上取，保证数据一致
        value.ProdInfoId ??= item.ProdInfoId;

        await _specValueRepository.AddAsync(value);
        await _specValueRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = value.Id }, "规格值创建成功");
    }

    /// <summary>
    /// 修改规格值
    /// </summary>
    public async Task<ApiResponse<object>> UpdateValueAsync(SpecValueUpdateDto dto)
    {
        var value = await _specValueRepository.GetByIdAsync(dto.Id);
        if (value == null)
        {
            return ApiResponse<object>.Fail("规格值不存在", 404);
        }

        _mapper.Map(dto, value);
        await _specValueRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = value.Id }, "规格值修改成功");
    }

    /// <summary>
    /// 删除规格值
    /// </summary>
    public async Task<ApiResponse<object>> DeleteValueAsync(long id)
    {
        var value = await _specValueRepository.GetByIdAsync(id);
        if (value == null)
        {
            return ApiResponse<object>.Fail("规格值不存在", 404);
        }

        _specValueRepository.Remove(value);
        await _specValueRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id }, "删除成功");
    }
}
