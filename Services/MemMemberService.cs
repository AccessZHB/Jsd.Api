using BCrypt.Net;
using Jsd.Api.Entities;
using Jsd.Api.Exceptions;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Customer;
using Jsd.Api.Models.System;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

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
    private readonly JwtService _jwtService;
    private readonly AppDbContext _db;
    private readonly ISecurityService _securityService;
    private readonly IConfiguration _configuration;

    public MemMemberService(
        IMemMemberRepository repository,
        JwtService jwtService,
        AppDbContext db,
        ISecurityService securityService,
        IConfiguration configuration)
    {
        _repository = repository;
        _jwtService = jwtService;   // 单例，用于签发小程序会员令牌
        _db = db;                   // 用于关联查询【客户等级】表（mem_member_level）动态取等级名与默认折扣
        _securityService = securityService;   // F-25：复用现有图形验证码服务（SVG + 一次性校验）
        _configuration = configuration;       // F-25：读取 Security:MemberLogin 安全策略
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
            query.Keyword, query.CustomerLevelId, query.Status, query.StartTime, query.EndTime, query.Page, query.PageSize);

        // 批量加载等级字典，动态回填 等级名称 + 默认折扣率（避免 N+1）
        var levelMap = await LoadLevelMapAsync(items.Select(m => m.CustomerLevelId));
        var list = items.Select(m => MapListItem(m, levelMap)).ToList();

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

        // 加载等级字典，动态回填 等级名称 + 默认折扣率
        var levelMap = await LoadLevelMapAsync(new[] { member.CustomerLevelId });

        return ApiResponse<CustomerDetailDto>.Success(MapDetail(member, levelMap));
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

        // 2. 密码必填 + 强度校验（与后台"登录安全策略"保持一致）
        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return ApiResponse<object>.Fail("登录密码不能为空（新增客户必须设置密码）");
        }
        var strengthError = await _securityService.ValidatePasswordStrengthAsync(dto.Password.Trim());
        if (strengthError != null)
        {
            return ApiResponse<object>.Fail(strengthError);
        }

        // 3. 自动生成会员编号
        var memberNo = await _repository.GenerateMemberNoAsync();

        // 4. BCrypt 加密密码（work factor 默认 12）
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim());

        // 5. 客户等级校验（折扣的唯一来源）：传入时必须存在且处于启用状态
        if (dto.CustomerLevelId.HasValue && dto.CustomerLevelId.Value > 0)
        {
            var levelExists = await _db.MemMemberLevels.AsNoTracking()
                .AnyAsync(l => l.Id == dto.CustomerLevelId.Value && l.Status == 1);
            if (!levelExists)
            {
                return ApiResponse<object>.Fail("所选客户等级不存在或已停用，请重新选择");
            }
        }

        // 6. 组装实体并保存（统计字段初始为 0，由订单联动回写）
        var member = new MemMember
        {
            MemberNo = memberNo,
            Name = dto.Name.Trim(),
            CompanyName = dto.CompanyName?.Trim(),
            // ⚠️ 折扣率不再落库：只持久化等级ID，折扣由等级的 default_discount 在取价时动态计算
            CustomerLevelId = dto.CustomerLevelId.HasValue && dto.CustomerLevelId.Value > 0
                ? dto.CustomerLevelId
                : null,
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

        // 3. 客户等级校验（折扣的唯一来源）：传入时必须存在且处于启用状态
        if (dto.CustomerLevelId.HasValue && dto.CustomerLevelId.Value > 0)
        {
            var levelExists = await _db.MemMemberLevels.AsNoTracking()
                .AnyAsync(l => l.Id == dto.CustomerLevelId.Value && l.Status == 1);
            if (!levelExists)
            {
                return ApiResponse<object>.Fail("所选客户等级不存在或已停用，请重新选择");
            }
        }

        // 4. 增量更新基础字段
        member.Name = dto.Name.Trim();
        member.CompanyName = dto.CompanyName?.Trim();
        // ⚠️ 折扣率不再落库：只更新等级ID，折扣由等级的 default_discount 在取价时动态计算
        member.CustomerLevelId = dto.CustomerLevelId.HasValue && dto.CustomerLevelId.Value > 0
            ? dto.CustomerLevelId
            : null;
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
            if (dto.Password.Trim().Length < 6)
            {
                return ApiResponse<object>.Fail("登录密码长度不能少于 6 位");
            }
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
    // 6.1 后台重置客户登录密码（BCrypt 加密覆盖，绝不落明文）
    // ============================================================
    public async Task<ApiResponse<object>> ResetPasswordAsync(long id, string newPassword)
    {
        // 1. 客户必须存在且未被删除
        var member = await _repository.GetByIdTrackedAsync(id);
        if (member == null || member.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("客户不存在", 404);
        }

        // 2. 新密码强度校验（与后台"登录安全策略"保持一致）
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return ApiResponse<object>.Fail("新密码不能为空");
        }
        var strengthError = await _securityService.ValidatePasswordStrengthAsync(newPassword.Trim());
        if (strengthError != null)
        {
            return ApiResponse<object>.Fail(strengthError);
        }

        // 3. BCrypt(cost=12) 加密覆盖原密码（绝不持久化明文）
        member.Password = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim(), workFactor: 12);

        await _repository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id }, "密码重置成功，请通知客户使用新密码登录");
    }

    // ============================================================
    // 6.2 小程序会员本人重置登录密码（校验旧密码 + BCrypt 加密覆盖）
    // ============================================================
    public async Task<ApiResponse<object>> SelfResetPasswordAsync(long memberId, string oldPassword, string newPassword)
    {
        // 1. 会员必须存在且未被删除
        var member = await _repository.GetByIdTrackedAsync(memberId);
        if (member == null || member.IsDeleted == 1)
        {
            return ApiResponse<object>.Fail("会员不存在", 404);
        }

        // 2. 旧密码校验（BCrypt 比对库里密文；空值也判错，避免空密码绕过）
        if (string.IsNullOrWhiteSpace(oldPassword) || !BCrypt.Net.BCrypt.Verify(oldPassword, member.Password))
        {
            return ApiResponse<object>.Fail("原登录密码不正确");
        }

        // 3. 新密码强度校验（与后台"登录安全策略"保持一致）
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return ApiResponse<object>.Fail("新密码不能为空");
        }
        var strengthError = await _securityService.ValidatePasswordStrengthAsync(newPassword.Trim());
        if (strengthError != null)
        {
            return ApiResponse<object>.Fail(strengthError);
        }

        // 4. 新密码不能与旧密码相同（避免无意义操作，降低撞库/重放风险）
        if (BCrypt.Net.BCrypt.Verify(newPassword.Trim(), member.Password))
        {
            return ApiResponse<object>.Fail("新密码不能与旧密码相同");
        }

        // 5. BCrypt(cost=12) 加密覆盖原密码（绝不持久化明文）
        member.Password = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim(), workFactor: 12);

        await _repository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = member.Id }, "密码重置成功，请使用新密码重新登录");
    }

    // ============================================================
    // 映射辅助方法（统一回填等级/状态中文名）
    // ============================================================
    /// <summary>
    /// 批量加载【客户等级】字典，避免逐条查库（N+1）。
    /// 供列表/详情在输出 DTO 时动态回填 等级名称 与 默认折扣率。
    /// </summary>
    private async Task<Dictionary<long, MemMemberLevel>> LoadLevelMapAsync(IEnumerable<long?> levelIds)
    {
        var ids = levelIds.Where(x => x.HasValue && x.Value > 0)
                          .Select(x => x!.Value)
                          .Distinct()
                          .ToList();
        if (ids.Count == 0) return new Dictionary<long, MemMemberLevel>();

        return await _db.MemMemberLevels.AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id);
    }

    private CustomerListItemDto MapListItem(MemMember m, Dictionary<long, MemMemberLevel>? levelMap = null)
    {
        MemMemberLevel? level = null;
        if (m.CustomerLevelId.HasValue && levelMap != null)
        {
            levelMap.TryGetValue(m.CustomerLevelId.Value, out level);
        }

        return new CustomerListItemDto
        {
            Id = m.Id,
            MemberNo = m.MemberNo,
            Name = m.Name,
            CompanyName = m.CompanyName,
            // 等级与折扣统一由【客户等级】表动态带出（客户表不再落库折扣率）
            CustomerLevelId = m.CustomerLevelId,
            LevelName = level?.LevelName ?? "未分级",
            DiscountRate = level?.DefaultDiscount ?? 1.00m,
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
    }

    // ============================================================
    // 7. 小程序会员登录（与后台管理员登录隔离）
    // ============================================================
    public async Task<ApiResponse<MemberLoginResultDto>> LoginAsync(MemberLoginDto dto)
    {
        var userName = (dto.UserName ?? string.Empty).Trim();
        var policy = LoadMemberLoginPolicy();
        var now = DateTime.Now;

        // ============================================================
        // ①【阶段四】锁定检查 —— 全流程最先执行，直接查库（多实例共享）
        //
        // 一旦命中锁定，直接抛业务异常终止：不校验验证码、不读明细、更**不进 BCrypt**。
        // 这是防 CPU DoS 的关键：BCrypt 是故意设计成慢速的重计算，
        // 若锁定期内还允许进来跑一次哈希，攻击者依然能把 CPU 打满。
        //
        // 只取 lock_until 一列（轻量查询），命中才抛；未锁定则继续走验证码。
        // ============================================================
        var lockUntil = await _db.MemMembers.AsNoTracking()
            .Where(m => m.UserName == userName && m.IsDeleted == 0)
            .Select(m => m.LockUntil)
            .FirstOrDefaultAsync();

        if (lockUntil.HasValue && lockUntil.Value > now)
        {
            throw new BusinessException(
                $"该账号已被锁定，请在 {lockUntil.Value:HH:mm} 后重试");
        }

        // ② 【F-25】图形验证码 —— 失败立即返回，绝不进入密码比对逻辑。
        //    VerifyCaptchaAsync 内部会在校验后**立即销毁**验证码（一次性，防重放）。
        var captchaOk = await _securityService.VerifyCaptchaAsync(
            dto.CaptchaKey,
            dto.CaptchaCode,
            new SecurityConfigDto { CaptchaEnabled = policy.CaptchaEnabled });

        if (!captchaOk)
        {
            return ApiResponse<MemberLoginResultDto>.Fail("验证码错误或已过期，请刷新后重试", 400);
        }

        // ② 按登录账号查会员（不存在/已删除统一提示，避免账号枚举）
        var member = await _repository.GetByUserNameAsync(userName);
        if (member == null || member.IsDeleted == 1)
        {
            return ApiResponse<MemberLoginResultDto>.Fail("账号或密码错误", 401);
        }

        // ③ BCrypt 校验密码（库里存的是密文）
        //    防御：存量数据可能存在空口令/非法哈希，BCrypt.Verify 会抛 ArgumentException，
        //    这里统一按"密码错误"处理，绝不让安全校验失败变成 500。
        var passwordMatched = false;
        if (!string.IsNullOrWhiteSpace(member.Password))
        {
            try
            {
                passwordMatched = BCrypt.Net.BCrypt.Verify(dto.Password, member.Password);
            }
            catch (ArgumentException)
            {
                passwordMatched = false;   // 哈希损坏/格式非法 → 视为不匹配
            }
        }

        if (!passwordMatched)
        {
            // ============================================================
            // ④【阶段四】失败累加 → 触发锁定（全部走数据库，原子更新）
            //
            // 关键：用 ExecuteUpdateAsync 在数据库侧做 `fail_count = fail_count + 1`，
            // 而不是"读出来 +1 再存回去"。后者在并发下会丢失更新
            // （两个请求同时读到 3，都写 4，实际应为 5），导致永远达不到锁定阈值。
            //
            // ⚠️ 这里**绝不能**再走 _repository.Update(member) + SaveChanges()：
            //    那会把实体上过期的 FailCount 写回去，覆盖掉原子自增的结果。
            // ============================================================
            await _db.MemMembers
                .Where(m => m.Id == member.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.FailCount, m => m.FailCount + 1));

            // 读回最新计数，判断是否达到阈值
            var failCount = await _db.MemMembers.AsNoTracking()
                .Where(m => m.Id == member.Id)
                .Select(m => m.FailCount)
                .FirstAsync();

            if (failCount >= policy.MaxLoginAttempts)
            {
                // 上锁 + 计数清零（解锁后重新获得完整次数）
                // 条件更新（FailCount >= 阈值）保证并发下也只会被设置一次，不会来回覆盖
                var newLockUntil = now.AddMinutes(policy.LockDurationMinutes);
                await _db.MemMembers
                    .Where(m => m.Id == member.Id && m.FailCount >= policy.MaxLoginAttempts)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.LockUntil, newLockUntil)
                        .SetProperty(m => m.FailCount, 0));
            }

            // 本次失败仍如实返回"账号或密码错误"（本次确实是密码错）；
            // 锁定在下一次访问时由第 ① 步拦截并提示"该账号已被锁定，请在 HH:mm 后重试"。
            return ApiResponse<MemberLoginResultDto>.Fail("账号或密码错误", 401);
        }

        // ========== ⑤【阶段四】登录成功：必须立刻清零失败计数 ==========
        // 否则用户改了密码 / 输错几次后再输对，仍会被残留计数拖入锁定。
        if (member.FailCount != 0 || member.LockUntil != null)
        {
            await _db.MemMembers
                .Where(m => m.Id == member.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.FailCount, 0)
                    .SetProperty(m => m.LockUntil, (DateTime?)null));
        }

        // 5. 状态校验：0-禁用 不允许登录
        if (member.Status != 1)
        {
            return ApiResponse<MemberLoginResultDto>.Fail("账号已禁用，请联系客服", 403);
        }

        // 6. 等级名称 + 折扣率：由【客户等级】表动态带出（客户表不再存等级枚举与折扣率）
        var levelName = "未分级";
        decimal discountRate = 1.00m;
        if (member.CustomerLevelId.HasValue)
        {
            var lv = await _db.MemMemberLevels.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == member.CustomerLevelId.Value);
            if (lv != null)
            {
                levelName = lv.LevelName;
                discountRate = lv.DefaultDiscount;
            }
        }

        // 7. 签发会员令牌（带 user_type=member，与管理员令牌区分）
        var (token, expiresIn) = _jwtService.GenerateMemberToken(member);

        return ApiResponse<MemberLoginResultDto>.Success(new MemberLoginResultDto
        {
            Token = token,
            ExpiresIn = expiresIn,
            MemberId = member.Id,
            MemberNo = member.MemberNo,
            Name = member.Name,
            Nickname = member.Name,
            LevelName = levelName,
            CustomerLevelId = member.CustomerLevelId,
            DiscountRate = discountRate,
            Balance = member.Balance
        });
    }

    /// <summary>
    /// 读取会员登录安全策略（appsettings: Security:MemberLogin）。
    /// 缺省值：验证码开启、5 次失败、锁定 30 分钟。
    /// </summary>
    private (bool CaptchaEnabled, int MaxLoginAttempts, int LockDurationMinutes) LoadMemberLoginPolicy()
    {
        var section = _configuration.GetSection("Security:MemberLogin");

        var captchaEnabled = section.GetValue("CaptchaEnabled", true);
        var maxAttempts = section.GetValue("MaxLoginAttempts", 5);
        var lockMinutes = section.GetValue("LockDurationMinutes", 30);

        if (maxAttempts < 1) maxAttempts = 5;
        if (lockMinutes < 1) lockMinutes = 30;

        return (captchaEnabled, maxAttempts, lockMinutes);
    }

    // ============================================================
    // 8. 当前登录会员资料
    // ============================================================
    public async Task<ApiResponse<MemberProfileDto>> GetProfileAsync(long memberId)
    {
        if (memberId <= 0)
        {
            return ApiResponse<MemberProfileDto>.Fail("会员ID不合法", 400);
        }

        var member = await _repository.GetByIdTrackedAsync(memberId);
        if (member == null || member.IsDeleted == 1)
        {
            return ApiResponse<MemberProfileDto>.Fail("会员不存在", 404);
        }

        // 加载等级字典，动态回填 等级名称 + 默认折扣率
        var levelMap = await LoadLevelMapAsync(new[] { member.CustomerLevelId });

        return ApiResponse<MemberProfileDto>.Success(MapProfile(member, levelMap));
    }

    private MemberProfileDto MapProfile(MemMember m, Dictionary<long, MemMemberLevel>? levelMap = null)
    {
        MemMemberLevel? level = null;
        if (m.CustomerLevelId.HasValue && levelMap != null)
        {
            levelMap.TryGetValue(m.CustomerLevelId.Value, out level);
        }

        return new MemberProfileDto
        {
            MemberId = m.Id,
            MemberNo = m.MemberNo,
            Name = m.Name,
            UserName = m.UserName,
            Phone = m.Phone,
            CompanyName = m.CompanyName,
            // 等级与折扣统一由【客户等级】表动态带出（客户表不再落库折扣率）
            CustomerLevelId = m.CustomerLevelId,
            LevelName = level?.LevelName ?? "未分级",
            DiscountRate = level?.DefaultDiscount ?? 1.00m,
            Balance = m.Balance,
            CreditLimit = m.CreditLimit,
            Status = m.Status
        };
    }

    private CustomerDetailDto MapDetail(MemMember m, Dictionary<long, MemMemberLevel>? levelMap = null)
    {
        MemMemberLevel? level = null;
        if (m.CustomerLevelId.HasValue && levelMap != null)
        {
            levelMap.TryGetValue(m.CustomerLevelId.Value, out level);
        }

        return new CustomerDetailDto
        {
            Id = m.Id,
            MemberNo = m.MemberNo,
            Name = m.Name,
            CompanyName = m.CompanyName,
            // 等级与折扣统一由【客户等级】表动态带出（客户表不再落库折扣率）
            CustomerLevelId = m.CustomerLevelId,
            LevelName = level?.LevelName ?? "未分级",
            DiscountRate = level?.DefaultDiscount ?? 1.00m,
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
}
