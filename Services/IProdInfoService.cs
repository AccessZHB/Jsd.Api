using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;

namespace Jsd.Api.Services;

/// <summary>
/// 商品（SPU）服务接口
/// </summary>
public interface IProdInfoService
{
    /// <summary>分页查询商品列表（名称/编码筛选，含分类名/供应商名/SKU价格区间/库存总量）</summary>
    Task<ApiResponse<PagedResult<ProdInfoDto>>> GetPagedListAsync(
        string? keyword, long? categoryId, long? supplierId, int? status, int page, int pageSize);

    /// <summary>商品详情：SPU + 图片 + 规格项(含规格值) + SKU 一次性返回</summary>
    Task<ApiResponse<ProdInfoDetailDto>> GetDetailAsync(long id);

    /// <summary>新增商品（事务聚合保存 SPU+规格+SKU+图片）</summary>
    Task<ApiResponse<object>> CreateAsync(ProdInfoCreateDto dto);

    /// <summary>修改商品（事务聚合更新，规格/SKU/图片全量替换）</summary>
    Task<ApiResponse<object>> UpdateAsync(ProdInfoUpdateDto dto);

    /// <summary>删除商品（级联清理 SKU/规格/图片）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    /// <summary>快速上下架</summary>
    Task<ApiResponse<object>> SetStatusAsync(long id, int status);
}
