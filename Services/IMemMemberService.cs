using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;

namespace Jsd.Api.Services;

/// <summary>
/// 会员/客户 服务接口
/// </summary>
public interface IMemMemberService
{
    /// <summary>分页查询客户列表（含等级/状态中文名、累计消费、最近订单）</summary>
    Task<ApiResponse<PagedResult<CustomerListItemDto>>> GetPagedListAsync(CustomerPageQuery query);

    /// <summary>获取客户详情（完整信息 + 等级中文名）</summary>
    Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(long id);

    /// <summary>分页查询某客户的订单历史</summary>
    Task<ApiResponse<PagedResult<CustomerOrderItemDto>>> GetOrderHistoryAsync(long id, int page, int pageSize);

    /// <summary>新增客户（自动生成会员编号 + BCrypt 密码加密）</summary>
    Task<ApiResponse<object>> CreateAsync(CustomerSaveDto dto);

    /// <summary>编辑客户（密码可选修改）</summary>
    Task<ApiResponse<object>> UpdateAsync(CustomerSaveDto dto);

    /// <summary>删除客户（软删除 + 未完成订单校验）</summary>
    Task<ApiResponse<object>> DeleteAsync(long id);

    // ===================== 小程序端（会员自助） =====================

    /// <summary>
    /// 小程序会员登录：按登录账号查会员 → BCrypt 校验密码 → 校验启用状态 → 签发会员 JWT。
    /// 与后台管理员登录（AuthService）完全隔离，两套账号体系不混用。
    /// </summary>
    Task<ApiResponse<MemberLoginResultDto>> LoginAsync(MemberLoginDto dto);

    /// <summary>
    /// 获取当前登录会员资料（「我的」页用）：会员编号、名称、等级、余额、折扣等。
    /// memberId 来自 JWT 的 member_id 声明。
    /// </summary>
    Task<ApiResponse<MemberProfileDto>> GetProfileAsync(long memberId);
}
