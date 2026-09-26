using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Services;

/// <summary>字典类型服务：分页/详情/下拉/增删改/启用停用</summary>
public interface IDictTypeService
{
    Task<ApiResponse<PagedResult<DictTypeDto>>> GetPagedAsync(DictTypeQueryDto query);

    Task<ApiResponse<DictTypeDto?>> GetByIdAsync(long id);

    /// <summary>字典类型下拉选项（仅启用状态）</summary>
    Task<ApiResponse<List<DictTypeOptionDto>>> GetOptionsAsync();

    Task<ApiResponse<long>> CreateAsync(CreateDictTypeDto dto);

    Task<ApiResponse<bool>> UpdateAsync(UpdateDictTypeDto dto);

    /// <summary>启用/停用字典类型（停用后其字典数据不再出现在缓存接口中）</summary>
    Task<ApiResponse<bool>> ChangeStatusAsync(long id, DictStatusDto dto);

    /// <summary>删除字典类型（同一事务内级联删除其下字典数据 + 清缓存）</summary>
    Task<ApiResponse<bool>> DeleteAsync(long id);
}
