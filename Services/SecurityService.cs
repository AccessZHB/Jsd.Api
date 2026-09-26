using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Jsd.Api.Entities;
using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 登录安全策略服务接口
/// 验证码生成/校验、连续失败锁定、密码强度校验、Token 刷新、修改密码、安全策略配置。
/// 【无 Redis 说明】本项目单机部署：验证码与 refresh jti 存进程内 IMemoryCache，
/// 失败计数/锁定落 sys_user 列（重启不丢）；多实例部署时这三处替换为 Redis 即可。
/// </summary>
public interface ISecurityService
{
    /// <summary>获取登录安全策略配置（sys_config.security.login，无记录返回默认值）</summary>
    Task<SecurityConfigDto> GetConfigAsync();

    /// <summary>修改登录安全策略配置（仅超管，Controller 校验）</summary>
    Task<ApiResponse<bool>> UpdateConfigAsync(SecurityConfigDto dto);

    /// <summary>生成图形验证码（SVG Base64，TTL 5 分钟，一次性消耗）</summary>
    Task<ApiResponse<CaptchaResultDto>> GenerateCaptchaAsync();

    /// <summary>校验验证码（无论成败都消耗，防爆破；策略未开启验证码时直接通过）</summary>
    Task<bool> VerifyCaptchaAsync(string? captchaKey, string? captchaCode, SecurityConfigDto config);

    /// <summary>校验密码强度（按策略配置），不满足返回错误消息，满足返回 null</summary>
    Task<string?> ValidatePasswordStrengthAsync(string password);

    /// <summary>账号是否处于锁定状态（返回剩余分钟数，未锁定返回 null）</summary>
    Task<int?> GetLockRemainingMinutesAsync(SysUser user);

    /// <summary>记录登录失败：失败计数+1（原子），达到阈值自动锁定；写入失败登录日志</summary>
    Task RecordLoginFailureAsync(SysUser? user, string inputUserName, string reason, string? ip, string? userAgent);

    /// <summary>记录登录成功：清零失败计数、回写最后登录时间/IP、写入成功登录日志</summary>
    Task RecordLoginSuccessAsync(SysUser user, string? ip, string? userAgent);

    /// <summary>手动解锁账号（重置失败计数与锁定时间，写操作日志；仅超管）</summary>
    Task<ApiResponse<bool>> UnlockAsync(long memberId);

    /// <summary>刷新 Token（旧 refreshToken 一次性核销防重放）</summary>
    Task<ApiResponse<TokenResultDto>> RefreshTokenAsync(RefreshTokenDto dto);

    /// <summary>修改密码：校验旧密码 + 强度，bcrypt(cost=12) 落库，pwd_version+1 强制全端下线</summary>
    Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordDto dto);
}

/// <summary>
/// 登录安全策略服务实现
/// </summary>
public class SecurityService : ISecurityService
{
    /// <summary>安全策略在 sys_config 里的配置键</summary>
    public const string ConfigKey = "security.login";

    /// <summary>验证码缓存前缀 + 有效期（文档：TTL 5 分钟）</summary>
    private const string CaptchaCachePrefix = "captcha:";
    private static readonly TimeSpan CaptchaTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ISysUserRepository _userRepository;
    private readonly ISysLoginLogRepository _loginLogRepository;
    private readonly CurrentUserService _currentUser;
    private readonly JwtService _jwtService;

    public SecurityService(
        AppDbContext db,
        IMemoryCache cache,
        ISysUserRepository userRepository,
        ISysLoginLogRepository loginLogRepository,
        CurrentUserService currentUser,
        JwtService jwtService)
    {
        _db = db;
        _cache = cache;
        _userRepository = userRepository;
        _loginLogRepository = loginLogRepository;
        _currentUser = currentUser;
        _jwtService = jwtService;
    }

    // ============================================================
    // 安全策略配置（sys_config.security.login，JSON 存取）
    // ============================================================

    /// <inheritdoc/>
    public async Task<SecurityConfigDto> GetConfigAsync()
    {
        var row = await _db.SysConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == ConfigKey);
        if (row == null || string.IsNullOrWhiteSpace(row.ConfigValue))
            return new SecurityConfigDto();   // 无记录返回默认值

        try
        {
            return JsonSerializer.Deserialize<SecurityConfigDto>(row.ConfigValue!,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new SecurityConfigDto();
        }
        catch
        {
            // 配置值损坏时降级为默认值，不阻塞登录
            return new SecurityConfigDto();
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UpdateConfigAsync(SecurityConfigDto dto)
    {
        var json = JsonSerializer.Serialize(dto,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var row = await _db.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == ConfigKey);
        if (row == null)
        {
            row = new SysConfig { ConfigKey = ConfigKey, ConfigValue = json, Remark = "登录安全策略" };
            await _db.SysConfigs.AddAsync(row);
        }
        else
        {
            row.ConfigValue = json;
        }
        await _db.SaveChangesAsync();

        // 操作日志由 [OperationLog] 过滤器统一异步采集（Controller 已标记），此处不再重复记录
        return ApiResponse<bool>.Success(true, "安全策略配置已保存");
    }

    // ============================================================
    // 图形验证码（SVG，无需 System.Drawing / 第三方包）
    // ============================================================

    /// <inheritdoc/>
    public Task<ApiResponse<CaptchaResultDto>> GenerateCaptchaAsync()
    {
        // 去掉易混淆字符（0/O、1/l/I）
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new string(Enumerable.Range(0, 4)
            .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
        var key = Guid.NewGuid().ToString("N");

        // 一次性会话：5 分钟后过期，校验时无论成败都删除
        _cache.Set(CaptchaCachePrefix + key, code, CaptchaTtl);

        var svg = BuildCaptchaSvg(code);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));

        return Task.FromResult(ApiResponse<CaptchaResultDto>.Success(new CaptchaResultDto
        {
            CaptchaKey = key,
            // data URI 前端 img 标签可直接用
            CaptchaImage = "data:image/svg+xml;base64," + base64
        }, "验证码已生成"));
    }

    /// <summary>生成带干扰线/噪点的 SVG 验证码图（120x40）</summary>
    private static string BuildCaptchaSvg(string code)
    {
        var rnd = new Random(code.GetHashCode());
        var sb = new System.Text.StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='40'>");
        sb.Append("<rect width='120' height='40' fill='#f5f7fa'/>");

        // 干扰线
        for (var i = 0; i < 4; i++)
        {
            sb.Append($"<line x1='{rnd.Next(0, 120)}' y1='{rnd.Next(0, 40)}' " +
                      $"x2='{rnd.Next(0, 120)}' y2='{rnd.Next(0, 40)}' " +
                      $"stroke='#{rnd.Next(160, 220):X2}{rnd.Next(160, 220):X2}{rnd.Next(160, 220):X2}' stroke-width='1'/>");
        }

        // 字符（随机旋转 + 灰度，保证可读性）
        for (var i = 0; i < code.Length; i++)
        {
            var x = 18 + i * 24;
            var y = 28 + rnd.Next(-3, 3);
            var rotate = rnd.Next(-20, 21);
            var gray = rnd.Next(40, 110);
            sb.Append($"<text x='{x}' y='{y}' font-size='24' font-family='Arial' font-weight='bold' " +
                      $"fill='rgb({gray},{gray},{gray})' transform='rotate({rotate} {x} {y})'>{code[i]}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public Task<bool> VerifyCaptchaAsync(string? captchaKey, string? captchaCode, SecurityConfigDto config)
    {
        // 策略未开启验证码：直接通过
        if (!config.CaptchaEnabled) return Task.FromResult(true);

        if (string.IsNullOrWhiteSpace(captchaKey) || string.IsNullOrWhiteSpace(captchaCode))
            return Task.FromResult(false);

        var cacheKey = CaptchaCachePrefix + captchaKey;
        if (!_cache.TryGetValue(cacheKey, out var expectedRaw) || expectedRaw is not string expected)
        {
            return Task.FromResult(false);   // 不存在=过期或非法 key
        }

        // 无论成败立即消耗（一次性，防重放爆破）
        _cache.Remove(cacheKey);

        return Task.FromResult(string.Equals(expected, captchaCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================
    // 密码强度 / 失败锁定
    // ============================================================

    /// <inheritdoc/>
    public async Task<string?> ValidatePasswordStrengthAsync(string password)
    {
        var config = await GetConfigAsync();
        if (string.IsNullOrEmpty(password) || password.Length < config.PasswordMinLength)
            return $"密码长度不能少于 {config.PasswordMinLength} 位";
        if (config.PasswordRequireUpper && !password.Any(char.IsUpper))
            return "密码必须包含大写字母";
        if (config.PasswordRequireLower && !password.Any(char.IsLower))
            return "密码必须包含小写字母";
        if (config.PasswordRequireDigit && !password.Any(char.IsDigit))
            return "密码必须包含数字";
        return null;
    }

    /// <inheritdoc/>
    public Task<int?> GetLockRemainingMinutesAsync(SysUser user)
    {
        if (user.LockUntil.HasValue && user.LockUntil.Value > DateTime.Now)
        {
            var minutes = (int)Math.Ceiling((user.LockUntil.Value - DateTime.Now).TotalMinutes);
            return Task.FromResult<int?>(Math.Max(minutes, 1));
        }
        return Task.FromResult<int?>(null);
    }

    /// <inheritdoc/>
    public async Task RecordLoginFailureAsync(SysUser? user, string inputUserName, string reason,
        string? ip, string? userAgent)
    {
        var config = await GetConfigAsync();
        string failReason = reason;

        if (user != null)
        {
            // 原子自增失败计数（UPDATE ... SET fail_count = fail_count + 1），并发安全
            await _db.SysUsers
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FailCount, u => u.FailCount + 1));

            // 重新读取计数判断是否达到锁定阈值
            var failCount = await _db.SysUsers.AsNoTracking()
                .Where(u => u.Id == user.Id)
                .Select(u => u.FailCount)
                .FirstAsync();

            if (failCount >= config.MaxLoginAttempts)
            {
                var lockUntil = DateTime.Now.AddMinutes(config.LockDurationMinutes);
                await _db.SysUsers
                    .Where(u => u.Id == user.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.LockUntil, lockUntil)
                        .SetProperty(u => u.FailCount, 0));
                failReason = $"{reason}，连续失败 {failCount} 次，账号已锁定 {config.LockDurationMinutes} 分钟";
            }
        }

        await _loginLogRepository.AddAsync(new SysLoginLog
        {
            MemberId = user?.Id,
            MemberName = inputUserName,
            LoginType = 1,
            IpAddress = ip ?? string.Empty,
            UserAgent = Truncate(userAgent, 500),
            Browser = UserAgentParser.ParseBrowser(userAgent),
            Os = UserAgentParser.ParseOs(userAgent),
            LoginLocation = string.Empty,   // IP 归属地解析降级：不引第三方服务，留空不阻塞
            Status = 0,
            FailReason = failReason,
            CreateTime = DateTime.Now
        });
    }

    /// <inheritdoc/>
    public async Task RecordLoginSuccessAsync(SysUser user, string? ip, string? userAgent)
    {
        // 成功登录：清零失败计数、解除锁定标记
        if (user.FailCount != 0 || user.LockUntil != null)
        {
            await _db.SysUsers
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailCount, 0)
                    .SetProperty(u => u.LockUntil, (DateTime?)null));
        }

        await _loginLogRepository.AddAsync(new SysLoginLog
        {
            MemberId = user.Id,
            MemberName = user.UserName,
            LoginType = 1,
            IpAddress = ip ?? string.Empty,
            UserAgent = Truncate(userAgent, 500),
            Browser = UserAgentParser.ParseBrowser(userAgent),
            Os = UserAgentParser.ParseOs(userAgent),
            LoginLocation = string.Empty,
            Status = 1,
            FailReason = string.Empty,
            LoginTime = DateTime.Now,
            CreateTime = DateTime.Now
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UnlockAsync(long memberId)
    {
        var user = await _userRepository.GetByIdAsync(memberId);
        if (user == null)
            return ApiResponse<bool>.Fail("账号不存在");

        await _db.SysUsers
            .Where(u => u.Id == memberId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailCount, 0)
                .SetProperty(u => u.LockUntil, (DateTime?)null));

        // 操作日志由 [OperationLog] 过滤器统一异步采集（Controller 已标记），此处不再重复记录
        return ApiResponse<bool>.Success(true, $"账号 {user.UserName} 已解锁");
    }

    // ============================================================
    // Token 刷新 / 修改密码
    // ============================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<TokenResultDto>> RefreshTokenAsync(RefreshTokenDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return ApiResponse<TokenResultDto>.Fail("refreshToken 不能为空");

        // 一次性核销：旧 refreshToken 用过即失效（防重放，文档九-4）
        var userId = _jwtService.ConsumeRefreshToken(dto.RefreshToken);
        if (userId == null)
            return ApiResponse<TokenResultDto>.Fail("refreshToken 已失效，请重新登录", 401);

        var user = await _userRepository.GetWithRoleAsync(userId.Value);
        if (user == null)
            return ApiResponse<TokenResultDto>.Fail("账号不存在，请重新登录", 401);
        if (user.Status != 1)
            return ApiResponse<TokenResultDto>.Fail("账号已被禁用，请联系管理员", 403);

        // 账号处于锁定状态也不允许刷新
        var lockMinutes = await GetLockRemainingMinutesAsync(user);
        if (lockMinutes != null)
            return ApiResponse<TokenResultDto>.Fail($"账号已锁定，请 {lockMinutes} 分钟后重新登录", 403);

        var (access, refresh, expiresIn, _) = _jwtService.GenerateTokenPair(user);
        return ApiResponse<TokenResultDto>.Success(new TokenResultDto
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresIn = expiresIn
        }, "刷新成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordDto dto)
    {
        var userId = _currentUser.UserId;
        if (userId <= 0)
            return ApiResponse<bool>.Fail("未登录或登录已过期", 401);

        if (dto.NewPassword != dto.ConfirmPassword)
            return ApiResponse<bool>.Fail("两次输入的新密码不一致");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<bool>.Fail("账号不存在", 401);

        // 1. 校验旧密码
        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.Password))
            return ApiResponse<bool>.Fail("旧密码不正确");

        // 2. 新密码强度（按安全策略配置校验）
        var strengthError = await ValidatePasswordStrengthAsync(dto.NewPassword);
        if (strengthError != null)
            return ApiResponse<bool>.Fail(strengthError);

        // 3. bcrypt(cost=12) 落库 + 密码版本号 +1（中间件据此拒绝所有旧 JWT，强制重新登录）
        var newPwdVersion = user.PwdVersion + 1;
        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        await _db.SysUsers
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Password, newHash)
                .SetProperty(u => u.PwdVersion, newPwdVersion));

        // 立即失效该用户的密码版本缓存（中间件缓存 60 秒，主动清除保证改密即刻下线）
        _cache.Remove($"pwd_version:{userId}");

        // 4. 操作日志由 [OperationLog] 过滤器统一异步采集（Controller 已标记），此处不再重复记录
        return ApiResponse<bool>.Success(true, "密码修改成功，请重新登录");
    }

    // ============================================================
    // 内部工具
    // ============================================================

    /// <summary>超长截断（数据库列宽保护）</summary>
    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? value
            : (value.Length <= maxLength ? value : value.Substring(0, maxLength));
}
