using Jsd.Api.Models.Common;
using Jsd.Api.Models.Supplier;

namespace Jsd.Api.Services;

/// <summary>
/// 供应商服务接口
/// </summary>
public interface ISupSupplierService
{
    /// <summary>分页查询供应商列表（名称模糊搜索 + 状态筛选）</summary>
    Task<ApiResponse<PagedResult<SupplierDto>>> GetPagedListAsync(string? keyword, int? status, int page, int pageSize);

    /// <summary>根据 ID 获取供应商详情</summary>
    Task<ApiResponse<SupplierDto>> GetByIdAsync(long id);

    /// <summary>新增供应商</summary>
    Task<ApiResponse<object>> CreateAsync(SupplierCreateDto dto);

    /// <summary>修改供应商信息</summary>
    Task<ApiResponse<object>> UpdateAsync(SupplierUpdateDto dto);

    /// <summary>删除供应商（有关联商品时拒绝删除）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    /// <summary>获取所有启用状态的供应商（商品录入下拉框用）</summary>
    Task<ApiResponse<List<SupplierDto>>> GetAllEnabledAsync();
}
