using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 客户（会员）管理接口
/// </summary>
[ApiController]
[Route("api/customer")]
[Authorize]  // 必须登录
public class CustomerController : ControllerBase
{
    private readonly IMemMemberService _memberService;

    public CustomerController(IMemMemberService memberService)
    {
        _memberService = memberService;
    }

    /// <summary>
    /// 分页查询客户列表（关键词/等级/状态/注册时间范围 筛选）
    /// 示例：GET /api/customer/page?page=1&pageSize=10&keyword=张&level=1&status=1
    /// </summary>
    [HttpGet("page")]
    public async Task<ApiResponse<PagedResult<CustomerListItemDto>>> GetPage([FromQuery] CustomerPageQuery query)
    {
        return await _memberService.GetPagedListAsync(query);
    }

    /// <summary>
    /// 获取客户详情（完整信息 + 等级中文名）
    /// 示例：GET /api/customer/1
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<CustomerDetailDto>> GetDetail(long id)
    {
        return await _memberService.GetDetailAsync(id);
    }

    /// <summary>
    /// 分页查询客户订单历史
    /// 示例：GET /api/customer/1/orders?page=1&pageSize=10
    /// </summary>
    [HttpGet("{id}/orders")]
    public async Task<ApiResponse<PagedResult<CustomerOrderItemDto>>> GetOrders(
        long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return await _memberService.GetOrderHistoryAsync(id, page, pageSize);
    }

    /// <summary>
    /// 新增客户（自动生成会员编号 + BCrypt 密码加密）
    /// 请求体：{ "name":"张三","userName":"zhangsan","password":"123456", ... }
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] CustomerSaveDto dto)
    {
        // [ApiController] + DTO 特性自动做非空/格式校验，不通过直接返回 400
        return await _memberService.CreateAsync(dto);
    }

    /// <summary>
    /// 编辑客户（密码可选，留空表示不修改）
    /// 请求体：{ "id":1, "name":"张三","userName":"zhangsan", "password":"" , ...}
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] CustomerSaveDto dto)
    {
        // 路由 id 与 主体 id 不一致时以路由为准，避免误改他人
        if (id != dto.Id)
        {
            dto.Id = id;
        }
        return await _memberService.UpdateAsync(dto);
    }

    /// <summary>
    /// 删除客户（软删除 + 未完成订单校验）
    /// 示例：DELETE /api/customer/1
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        return await _memberService.DeleteAsync(id);
    }
}
