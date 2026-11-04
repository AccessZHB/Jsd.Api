using Jsd.Api.Models.Cart;
using Jsd.Api.Models.Common;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 购物车接口（小程序）
/// 路由前缀：/api/cart
///
/// 【会员维度隔离】全部按 JWT 中的 member_id 过滤，绝不读写他人购物车。
/// 【核心规则】唯一键 = 商品 + SKU + 是否需要加工 + 验光数据哈希；
///            任一项不同（典型：需要加工 vs 只发片）必须两条记录，绝不合并数量。
///
/// 接口清单：
///   1. GET  /api/cart/list            购物车列表（含商品详情 / 加工标签 / 验光 / 金额统计）
///   2. POST /api/cart/add             加入购物车（同键累加，不同键新增）
///   3. POST /api/cart/update-qty      修改某一行数量
///   4. POST /api/cart/delete          删除单行
///   5. POST /api/cart/clear           清空购物车
///   6. POST /api/cart/toggle-select   切换单行勾选
///   7. POST /api/cart/toggle-all      全选 / 全不选
/// </summary>
[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// 1. 购物车列表。
    /// 示例：GET /api/cart/list
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<CartListDto>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<CartListDto>> List()
    {
        return await _cartService.GetListAsync();
    }

    /// <summary>
    /// 2. 加入购物车。
    /// 合并判定：member_id + 商品 + SKU + 是否需要加工 + 验光数据 全部一致才累加数量；
    /// 否则（例如一个「需要加工」、一个「只发片」）插入新记录。
    /// 示例：POST /api/cart/add
    ///   body: {
    ///     "skuId": 13,
    ///     "quantity": 1,
    ///     "processingType": 1,
    ///     "prescription": {
    ///       "left":  { "sphere": "-1.75", "cylinder": "-0.50", "axis": "180", "pd": "32", "height": "" },
    ///       "right": { "sphere": "-2.00", "cylinder": "-0.75", "axis": "175", "pd": "32", "height": "" }
    ///     }
    ///   }
    /// </summary>
    [HttpPost("add")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<long>> Add([FromBody] CartAddDto dto)
    {
        return await _cartService.AddAsync(dto);
    }

    /// <summary>
    /// 3. 修改数量。示例：POST /api/cart/update-qty  body: { "id": 1, "quantity": 3 }
    /// </summary>
    [HttpPost("update-qty")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<bool>> UpdateQty([FromBody] CartUpdateQtyDto dto)
    {
        return await _cartService.UpdateQtyAsync(dto);
    }

    /// <summary>
    /// 4. 删除单行。示例：POST /api/cart/delete  body: { "id": 1 }
    /// </summary>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<bool>> Delete([FromBody] CartIdDto dto)
    {
        return await _cartService.DeleteAsync(dto.Id);
    }

    /// <summary>
    /// 5. 清空购物车。示例：POST /api/cart/clear
    /// </summary>
    [HttpPost("clear")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<bool>> Clear()
    {
        return await _cartService.ClearAsync();
    }

    /// <summary>
    /// 6. 切换单行勾选。示例：POST /api/cart/toggle-select  body: { "id": 1, "selected": true }
    /// </summary>
    [HttpPost("toggle-select")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<bool>> ToggleSelect([FromBody] CartToggleDto dto)
    {
        return await _cartService.ToggleSelectAsync(dto);
    }

    /// <summary>
    /// 7. 全选 / 全不选。示例：POST /api/cart/toggle-all  body: { "selected": true }
    /// </summary>
    [HttpPost("toggle-all")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<bool>> ToggleAll([FromBody] CartToggleAllDto dto)
    {
        return await _cartService.ToggleAllAsync(dto.Selected);
    }
}
