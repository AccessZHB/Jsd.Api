using Jsd.Api.Models.Cart;
using Jsd.Api.Models.Common;

namespace Jsd.Api.Services;

/// <summary>
/// 购物车服务（小程序 /api/cart/*）
///
/// 【核心业务规则】合并判定 = 商品 + SKU + 是否需要加工 + 完整验光数据（哈希）全部一致。
/// 任意一项不同（典型：一个「需要加工」一个「只发片」）必须插入新行，绝不累加数量。
/// </summary>
public interface ICartService
{
    /// <summary>获取当前会员购物车（含商品详情、加工标签、验光数据、金额统计）</summary>
    Task<ApiResponse<CartListDto>> GetListAsync();

    /// <summary>加入购物车（同 key 累加数量，不同 key 插入新行）</summary>
    Task<ApiResponse<long>> AddAsync(CartAddDto dto);

    /// <summary>修改某一行的数量</summary>
    Task<ApiResponse<bool>> UpdateQtyAsync(CartUpdateQtyDto dto);

    /// <summary>删除单行</summary>
    Task<ApiResponse<bool>> DeleteAsync(long id);

    /// <summary>清空购物车</summary>
    Task<ApiResponse<bool>> ClearAsync();

    /// <summary>切换单行勾选状态</summary>
    Task<ApiResponse<bool>> ToggleSelectAsync(CartToggleDto dto);

    /// <summary>全选 / 全不选</summary>
    Task<ApiResponse<bool>> ToggleAllAsync(bool selected);
}
