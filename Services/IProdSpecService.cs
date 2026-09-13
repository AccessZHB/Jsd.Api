using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;

namespace Jsd.Api.Services;

/// <summary>
/// 商品规格服务接口（规格项 + 规格值）
/// </summary>
public interface IProdSpecService
{
    // ---------- 规格项 ----------
    /// <summary>根据商品ID获取该商品的所有规格项</summary>
    Task<ApiResponse<List<SpecItemDto>>> GetItemsByProductAsync(long prodInfoId);

    /// <summary>新增规格项</summary>
    Task<ApiResponse<object>> CreateItemAsync(SpecItemCreateDto dto);

    /// <summary>修改规格项（改名/类型/排序）</summary>
    Task<ApiResponse<object>> UpdateItemAsync(SpecItemUpdateDto dto);

    /// <summary>删除规格项（级联删除其下规格值）</summary>
    Task<ApiResponse<object>> DeleteItemAsync(long id);

    // ---------- 规格值 ----------
    /// <summary>根据规格项ID获取该规格项的所有规格值</summary>
    Task<ApiResponse<List<SpecValueDto>>> GetValuesByItemAsync(long specId);

    /// <summary>新增规格值</summary>
    Task<ApiResponse<object>> CreateValueAsync(SpecValueCreateDto dto);

    /// <summary>修改规格值</summary>
    Task<ApiResponse<object>> UpdateValueAsync(SpecValueUpdateDto dto);

    /// <summary>删除规格值</summary>
    Task<ApiResponse<object>> DeleteValueAsync(long id);
}
