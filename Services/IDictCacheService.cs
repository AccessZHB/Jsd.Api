using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Services;

/// <summary>字典缓存服务：缓存读写 + 失效 + 预热</summary>
public interface IDictCacheService
{
    /// <summary>
    /// 按字典类型编码读取启用数据列表（缓存接口）。
    /// 命中缓存直接返回；未命中回源数据库并回写缓存（空结果也缓存，TTL 更短）。
    /// </summary>
    Task<List<DictDataItem>> GetByDictTypeAsync(string dictType);

    /// <summary>失效指定字典类型的缓存（增删改字典数据后调用）</summary>
    Task RemoveAsync(string dictType);

    /// <summary>清空全部字典缓存</summary>
    Task<ApiResponse<DictCacheResultDto>> ClearAllAsync();

    /// <summary>重新加载指定字典类型的缓存（返回影响的条数）</summary>
    Task<ApiResponse<DictCacheResultDto>> RefreshAsync(string dictType);

    /// <summary>应用启动时预热：把所有启用中的字典类型批量装载进缓存</summary>
    Task PreloadAllAsync();
}
