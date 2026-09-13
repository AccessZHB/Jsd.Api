using BCrypt.Net;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 会员/客户 服务实现。
///
/// 一、关键业务逻辑
///   1. 会员编号自动生成：M + yyyyMMddHHmmss + 4位随机数（仓储层保证唯一）。
///   2. 密码安全：新增/编辑的明文密码统一用 BCrypt 加密存储，绝不落明文；编辑时密码留空表示不修改。
///   3. 等级名称映射：后端在输出 DTO 时回填 LevelName / StatusName（与前端映射保持一致，避免前端硬编码）。
///   4. 删除前校验：存在"未发货订单"（order_status ∈ {0,1}）的客户禁止删除，避免订单悬挂。
///   5. 账号唯一：登录账号 username 全局唯一，新增/编辑均做重复校验。
///
/// 二、累计统计联动
///   mem_member 的 total_consumption / order_count / completed_order_count / last_order_no / last_order_time
///   由订单模块（OrderService）在【支付】与【完成】时自动回写，本服务不主动维护。
///
/// 三、软删除
///   删除仅置 is_deleted = 1，列表/详情查询统一过滤，历史订单仍可关联查询。
/// </summary>
public class MemMemberService : IMemMemberService
{
    private readonly IMemMemberRepository _repository;

    public MemMemberService(IMemMemberRepository repository)
    {
        _repository = repository;
    }

    // ============================================================
    // 1. 分页查询客户列表
    // ============================================================
    public async Task<ApiResponse<PagedResult<CustomerListItemDto>>> GetPagedListAsync(CustomerPageQuery query)
    {
        // 参数兜底，防止前端传非法值
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 100) query.PageSize = 10;

        var (items, total) = await _repository.GetPagedListAsync(
            query.Keyword, query.Level, query.Status, query.StartTime, query.EndTime, query.Page, query.PageSize);

        var list = items.Select(MapListItem).ToList();

        return ApiResponse<PagedResult<CustomerListItemDto>>.Success(
            PagedResult<CustomerListItemDto>.Create(list, total, query.Page, query.PageSize));
    }

    // ============================================================
    // 2. 获取客户详情
    // ============================================================
    public async Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(long id)
    {
        var member = await _repository.GetByIdTrackedAsync(id);
        if (member == null)
        {
            return ApiResponse<CustomerDetailDto>.Fail("客户不存在", 404);
        }

        return ApiResponse<CustomerDetailDto>.Success(MapDetail(member));
    }

    // ============================================================
    // 3. 客户订单历史（分页）
    // ============================================================
    public async Task<ApiResponse<PagedResult<CustomerOrderItemDto>>> GetOrderHistoryAsync(long id, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        // 客户必须存在
        if (await _repository.GetByIdTrackedAsync(id) == null)
        {
            return ApiResponse<PagedResult<CustomerOrderItemDto>>.Fail("客户不存在", 404);
        }

        // 直接跨表查询 trx_order（订单表 buyer_id 即会员 id），按创建时间倒序（最近订单在前）
        var (orders, total) = await _repository.GetOrderHistoryAsync(id, page, pageSize);

        var list = orders.Select(o => new CustomerOrderItemDto
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            PayAmount = o.PayAmount,
            OrderStatus = o.OrderStatus,
            OrderStatusName = CustomerMaps.GetOrderStatusName(o.OrderStatus),
            CreateTime = o.CreateTime
        }).ToList();

        return ApiResponse<PagedResult<CustomerOrderItemDto>>.Success(
            PagedResult<CustomerOrderItemDto>.Create(list, total, page, pageSize));
    }

    // ============================================================
    // 4. 新增客户
    // ============================================================
    public async Task<ApiResponse<object>> CreateAsync(CustomerSaveDto dto)
    {
        // 1. 登录账号重复校验
        if (await _repository.GetByUserNameAsync(dto.UserName) != null)
        {
            return ApiResponse<object>.Fail("登录账号已存在，请更换");
        }

        // 2. 密码必填（新增必须有登录密码）
        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return ApiResponse<object>.Fail("登录密码不能为空（新增客户必须设置密码）");
        }

        // 3. 自动生成会员编号
        var memberNo = await _repository.GenerateMemberNoAsync();

        // 4. BCrypt 加密密码（work factor 默认 12）
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim());

        // 5. 组装实体并保存（统计字段初始为 0，由订单联动回写）
        var member = new MemMember
        {
            MemberNo = memberNo,
            Name = dto.Name.Trim(),
            CompanyName = dto.CompanyName?.Trim(),
            Level = dto.Level,
            DiscountRate = dto.DiscountRate,
            CreditLimit = dto.CreditLimit,
            AccountPeriod = dto.AccountPeriod,
            UserName = dto.UserName.Trim(),
            Password = passwordHash,
            Phone = dto.Phone?.Trim(),
            Email = dto.Email?.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            Address = dto.Address?.Trim(),
            Remark = dto.Remark?.Trim(),
            Status = dto.Status
        };

        await _repository.AddAsync(member);
        await _repository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = member.Id, memberNo }, "客户创建成功");
    }

    // ============================================================
    // 5. 编辑客户（密码可选）
    // ============================================================
    public async Task<ApiResponse<object>> UpdateAsync(CustomerSaveDto dto)
    {
        // 1. 客户必须存在
        var member = await _repository.GetByIdTrackedAsync(dto.Id);
        if (member == null)
        {
            return ApiResponse<object>.Fail("客户不存在", 404);
        }

        // 2. 登录账号重复校验（排除自己）
        var sameName = await _repository.GetByUserNameAsync(dto.UserName);
        if (sameName != null && sameName.Id != dto.Id)
        {
            return ApiResponse<object>.Fail("登录账号已存在，请更换");
        }

        // 3. 增量更新基础字段
        member.Name = dto.Name.Trim();
        member.CompanyName = dto.CompanyName?.Trim();
        member.Level = dto.Level;
        member.DiscountRate = dto.DiscountRate;
        member.CreditLimit = dto.CreditLimit;
        member.AccountPeriod = dto.AccountPeriod;
        member.UserName = dto.UserName.Trim();
        member.Phone = dto.Phone?.Trim();
        member.Email = dto.Email?.Trim();
        member.ContactPerson = dto.ContactPerson?.Trim();
        member.Address = dto.Address?.Trim();
        member.Remark = dto.Remark?.Trim();
        member.Status = dto.Status;

        // 4. 密码可选：仅在传入非空时重新加密（留空 = 不修改原密码）
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            member.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim());
        }

        await _repository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = member.Id }, "客户修改成功");
    }

    // ============================================================
    // 6. 删除客户（软删除 + 未完成订单校验）
    // ============================================================
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var member = await _repository.GetByIdTrackedAsync(id);
        if (member == null)
        {
            return ApiResponse<object>.Fail("客户不存在", 404);
        }

        // 存在未发货（待付款/待发货）订单时禁止删除，防止订单悬挂
        if (await _repository.HasUnfinishedOrderAsync(id))
        {
            return ApiResponse<object>.Fail("该客户存在未发货订单，请先处理完订单后再删除");
        }

        // 软删除：仅置标志位，保留历史数据
        member.IsDeleted = 1;
        await _repository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id }, "删除成功");
    }

    // ============================================================
    // 映射辅助方法（统一回填等级/状态中文名）
    // ============================================================
    private static CustomerListItemDto MapListItem(MemMember m) => new()
    {
        Id = m.Id,
        MemberNo = m.MemberNo,
        Name = m.Name,
        CompanyName = m.CompanyName,
        Level = m.Level,
        LevelName = CustomerMaps.GetLevelName(m.Level),
        DiscountRate = m.DiscountRate,
        CreditLimit = m.CreditLimit,
        AccountPeriod = m.AccountPeriod,
        TotalConsumption = m.TotalConsumption,
        OrderCount = m.OrderCount,
        CompletedOrderCount = m.CompletedOrderCount,
        LastOrderNo = m.LastOrderNo,
        LastOrderTime = m.LastOrderTime,
        UserName = m.UserName,
        Phone = m.Phone,
        Status = m.Status,
        StatusName = CustomerMaps.GetStatusName(m.Status),
        CreateTime = m.CreateTime
    };

    private static CustomerDetailDto MapDetail(MemMember m) => new()
    {
        Id = m.Id,
        MemberNo = m.MemberNo,
        Name = m.Name,
        CompanyName = m.CompanyName,
        Level = m.Level,
        LevelName = CustomerMaps.GetLevelName(m.Level),
        DiscountRate = m.DiscountRate,
        CreditLimit = m.CreditLimit,
        AccountPeriod = m.AccountPeriod,
        TotalConsumption = m.TotalConsumption,
        OrderCount = m.OrderCount,
        CompletedOrderCount = m.CompletedOrderCount,
        LastOrderNo = m.LastOrderNo,
        LastOrderTime = m.LastOrderTime,
        UserName = m.UserName,
        Phone = m.Phone,
        Status = m.Status,
        StatusName = CustomerMaps.GetStatusName(m.Status),
        CreateTime = m.CreateTime,
        Email = m.Email,
        ContactPerson = m.ContactPerson,
        Address = m.Address,
        Remark = m.Remark,
        UpdateTime = m.UpdateTime
    };
}
