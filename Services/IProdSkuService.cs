using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;

namespace Jsd.Api.Services;

/// <summary>
/// 商品 SKU 服务接口
/// </summary>
public interface IProdSkuService
{
    /// <summary>根据商品ID获取该商品的所有SKU</summary>
    Task<ApiResponse<List<ProdSkuDto>>> GetListByProductAsync(long prodInfoId);

    /// <summary>根据 ID 获取SKU详情</summary>
    Task<ApiResponse<ProdSkuDto>> GetByIdAsync(long id);

    /// <summary>新增单个SKU</summary>
    Task<ApiResponse<object>> CreateAsync(ProdSkuCreateDto dto);

    /// <summary>修改SKU（价格/库存/编码等）</summary>
    Task<ApiResponse<object>> UpdateAsync(ProdSkuUpdateDto dto);

    /// <summary>删除SKU</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);
}
