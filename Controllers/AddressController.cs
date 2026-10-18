using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Order;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Controllers;

/// <summary>
/// 收货地址接口（小程序确认订单页）
/// 路由前缀：/api/address
/// 会员维度隔离：所有读写都按当前登录会员（JWT member_id）过滤，绝不越权访问他人地址。
///
/// 接口清单：
///   1. GET    /api/address/list   当前会员地址列表（默认地址排前）
///   2. POST   /api/address/save   新增/修改地址（Name/Phone/Province/City/District/Detail/IsDefault）
///   3. POST   /api/address/delete 删除地址（软删除；校验归属；删默认地址时自动提升其他地址为默认）
/// </summary>
[ApiController]
[Route("api/address")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _currentUser;

    public AddressController(AppDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ============================================================
    // 1. 地址列表
    // ============================================================

    /// <summary>
    /// 1. 当前会员收货地址列表（默认地址排最前）。
    /// 示例：GET /api/address/list
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<List<AddressDto>>), StatusCodes.Status200OK)]
    public async Task<ApiResponse<List<AddressDto>>> List()
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<List<AddressDto>>.Fail("未获取到登录会员信息", 401);
        }

        var list = await _db.MemMemberAddresses.AsNoTracking()
            .Where(a => a.MemberId == memberId && a.IsDeleted == 0)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .Select(a => new AddressDto
            {
                Id = a.Id,
                MemberId = a.MemberId,
                Name = a.Name,
                Phone = a.Phone,
                Province = a.Province,
                City = a.City,
                District = a.District,
                Detail = a.Detail,
                IsDefault = a.IsDefault == 1
            })
            .ToListAsync();

        return ApiResponse<List<AddressDto>>.Success(list);
    }

    // ============================================================
    // 2. 新增 / 修改地址
    // ============================================================

    /// <summary>
    /// 2. 新增 / 修改收货地址（Id=0 新增，&gt;0 修改）。
    /// 设为默认时，自动清除该会员其他默认地址；若会员无任何地址，首条自动设为默认。
    /// 示例：POST /api/address/save
    ///   body: { "id": 0, "name": "张三", "phone": "138...", "province":"陕西", "city":"西安", "district":"雁塔区", "detail":"...", "isDefault": true }
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ApiResponse<long>> Save([FromBody] AddressSaveDto dto)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<long>.Fail("未获取到登录会员信息", 401);
        }

        MemMemberAddress? entity;
        if (dto.Id > 0)
        {
            entity = await _db.MemMemberAddresses
                .FirstOrDefaultAsync(a => a.Id == dto.Id && a.MemberId == memberId && a.IsDeleted == 0);
            if (entity == null)
            {
                return ApiResponse<long>.Fail("地址不存在或不属于当前会员");
            }
        }
        else
        {
            entity = new MemMemberAddress
            {
                MemberId = memberId,
                IsDeleted = 0,
                CreateTime = DateTime.Now
            };
            await _db.MemMemberAddresses.AddAsync(entity);
        }

        entity.Name = dto.Name.Trim();
        entity.Phone = dto.Phone.Trim();
        entity.Province = (dto.Province ?? string.Empty).Trim();
        entity.City = (dto.City ?? string.Empty).Trim();
        entity.District = (dto.District ?? string.Empty).Trim();
        entity.Detail = dto.Detail.Trim();
        entity.IsDefault = dto.IsDefault ? 1 : 0;

        // 设为默认：清除该会员其他默认地址（唯一默认约束由业务层保证）
        if (entity.IsDefault == 1)
        {
            await _db.MemMemberAddresses
                .Where(a => a.MemberId == memberId && a.Id != entity.Id && a.IsDefault == 1 && a.IsDeleted == 0)
                .ExecuteUpdateAsync(a => a.SetProperty(p => p.IsDefault, 0));
        }
        // 非默认且会员当前无默认地址：首条自动设为默认
        else
        {
            var hasDefault = await _db.MemMemberAddresses
                .AnyAsync(a => a.MemberId == memberId && a.Id != entity.Id && a.IsDefault == 1 && a.IsDeleted == 0);
            if (!hasDefault)
            {
                entity.IsDefault = 1;
            }
        }

        await _db.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, dto.Id > 0 ? "地址已更新" : "地址已保存");
    }

    // ============================================================
    // 3. 删除地址
    // ============================================================

    /// <summary>
    /// 3. 删除收货地址（软删除）。
    /// 从 JWT 取当前会员 member_id，校验地址归属，杜绝越权删除他人地址；
    /// 若删除的是默认地址且仍有其他地址，自动将最新一条设为默认，保证会员始终有默认地址。
    /// 示例：POST /api/address/delete
    ///   body: { "id": 12 }
    /// </summary>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ApiResponse<bool>> Delete([FromBody] AddressDeleteDto dto)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        // 仅允许删除当前会员自己的、未删除的地址
        var entity = await _db.MemMemberAddresses
            .FirstOrDefaultAsync(a => a.Id == dto.Id && a.MemberId == memberId && a.IsDeleted == 0);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("地址不存在或不属于当前会员", 404);
        }

        var wasDefault = entity.IsDefault == 1;

        // 软删除：置 is_deleted=1（与 list/save 的过滤口径一致）
        entity.IsDeleted = 1;

        // 删的是默认地址，且还有其他地址：自动把最新一条提升为默认，保证始终有默认地址
        if (wasDefault)
        {
            var next = await _db.MemMemberAddresses
                .Where(a => a.MemberId == memberId && a.Id != entity.Id && a.IsDeleted == 0)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();
            if (next != null)
            {
                next.IsDefault = 1;
            }
        }

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, wasDefault ? "默认地址已删除，已自动将其他地址设为默认" : "地址已删除");
    }
}
