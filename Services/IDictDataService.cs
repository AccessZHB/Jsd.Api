using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Services;

/// <summary>字典数据服务：分页/详情/增删改/启用停用 + 按类型编码读取（走缓存）</summary>
public interface IDictDataService
{
    Task<ApiResponse<PagedResult<DictDataDto>>> GetPagedAsync(DictDataQueryDto query);

    Task<ApiResponse<DictDataDto?>> GetByIdAsync(long id);

    Task<ApiResponse<long>> CreateAsync(CreateDictDataDto dto);

    Task<ApiResponse<bool>> UpdateAsync(UpdateDictDataDto dto);

    /// <summary>启用/停用字典数据</summary>
    Task<ApiResponse<bool>> ChangeStatusAsync(long id, DictStatusDto dto);

    Task<ApiResponse<bool>> DeleteAsync(long id);

    /// <summary>按字典类型编码获取启用数据（核心缓存接口，内部走 IMemoryCache）</summary>
    Task<ApiResponse<List<DictDataItem>>> GetByDictTypeAsync(string dictType);
}
