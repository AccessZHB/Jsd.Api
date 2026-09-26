using AutoMapper;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 字典缓存服务实现：读缓存 → 回源 → 写缓存，以及增删改后的缓存失效、启动预热。
/// </summary>
public class DictCacheService : IDictCacheService
{
    private readonly IDictDataRepository _dataRepository;
    private readonly DictCacheHelper _cacheHelper;
    private readonly IMapper _mapper;

    public DictCacheService(
        IDictDataRepository dataRepository,
        DictCacheHelper cacheHelper,
        IMapper mapper)
    {
        _dataRepository = dataRepository;
        _cacheHelper = cacheHelper;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<List<DictDataItem>> GetByDictTypeAsync(string dictType)
    {
        if (string.IsNullOrWhiteSpace(dictType)) return new List<DictDataItem>();

        // 1) 先读缓存
        var cached = await _cacheHelper.GetAsync(dictType);
        if (cached != null) return cached;

        // 2) 未命中：查数据库（只取启用状态）
        var entities = await _dataRepository.GetEnabledListByDictTypeAsync(dictType);

        // 3) 回写缓存（空列表也写，TTL 更短，防缓存穿透）
        var items = _mapper.Map<List<DictDataItem>>(entities);
        await _cacheHelper.SetAsync(dictType, items);

        return items;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string dictType)
    {
        await _cacheHelper.RemoveAsync(dictType);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DictCacheResultDto>> ClearAllAsync()
    {
        await _cacheHelper.RemoveAllAsync();

        return ApiResponse<DictCacheResultDto>.Success(new DictCacheResultDto
        {
            Action = "clear",
            DictType = null,
            Count = 0
        }, "字典缓存已全部清空");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DictCacheResultDto>> RefreshAsync(string dictType)
    {
        if (string.IsNullOrWhiteSpace(dictType))
        {
            return ApiResponse<DictCacheResultDto>.Fail("字典类型编码不能为空");
        }

        var entities = await _dataRepository.GetEnabledListByDictTypeAsync(dictType);
        var items = _mapper.Map<List<DictDataItem>>(entities);

        // 覆盖写回缓存；顺手把可能存在的旧（空）缓存清掉，避免 TTL 窗口内读到脏数据
        await _cacheHelper.RemoveAsync(dictType);
        await _cacheHelper.SetAsync(dictType, items);

        return ApiResponse<DictCacheResultDto>.Success(new DictCacheResultDto
        {
            Action = "refresh",
            DictType = dictType,
            Count = items.Count
        }, $"字典 [{dictType}] 缓存已刷新，共 {items.Count} 条");
    }

    /// <inheritdoc/>
    public async Task PreloadAllAsync()
    {
        var types = await _dataRepository.GetEnabledTypeCodesAsync();

        foreach (var code in types)
        {
            try
            {
                await GetByDictTypeAsync(code);
            }
            catch
            {
                // 预热是"锦上添花"，任何异常都不能影响应用启动，忽略即可
            }
        }
    }
}
