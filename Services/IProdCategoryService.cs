using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;

namespace Jsd.Api.Services;

/// <summary>
/// 商品分类服务接口
/// </summary>
public interface IProdCategoryService
{
    /// <summary>获取分类树形结构（全部分类，递归组装）</summary>
    Task<ApiResponse<List<CategoryTreeNodeDto>>> GetTreeAsync();

    /// <summary>分页查询分类列表（支持按名称搜索，扁平结构）</summary>
    Task<ApiResponse<PagedResult<CategoryDto>>> GetPagedListAsync(string? keyword, int page, int pageSize);

    /// <summary>根据 ID 获取分类详情</summary>
    Task<ApiResponse<CategoryDto>> GetByIdAsync(long id);

    /// <summary>新增分类</summary>
    Task<ApiResponse<object>> CreateAsync(CategoryCreateDto dto);

    /// <summary>修改分类</summary>
    Task<ApiResponse<object>> UpdateAsync(CategoryUpdateDto dto);

    /// <summary>删除分类（有子分类或关联商品时拒绝删除）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);
}
