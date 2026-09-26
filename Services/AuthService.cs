using Jsd.Api.Entities;
using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 认证服务实现（系统管理模块改造版：验证码 / 登录锁定 / 登录日志 / access+refresh 双令牌）
/// 接口定义见 IAuthService.cs
/// </summary>
public class AuthService : IAuthService
{
    private readonly ISysUserRepository _userRepository;
    private readonly ISecurityService _securityService;
    private readonly JwtService _jwtService;

    public AuthService(
        ISysUserRepository userRepository,
        ISecurityService securityService,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _securityService = securityService;
        _jwtService = jwtService;
    }

    /// <summary>
    /// 登录流程（每次尝试均写 sys_login_log，见各分支的 RecordLogin* 调用）：
    /// 1. 验证码校验（策略开启时，一次性消耗）
    /// 2. 按账号查用户（带出角色）
    /// 3. 账号状态校验（禁用）→ 锁定状态校验
    /// 4. BCrypt 校验密码
    /// 5. 成功：清零失败计数 + 成功登录日志 + 双令牌；失败：失败计数+1 + 失败登录日志（达阈值自动锁定）
    /// </summary>
    public async Task<ApiResponse<LoginResultDto>> LoginAsync(LoginDto dto, string? clientIp, string? userAgent)
    {
        var config = await _securityService.GetConfigAsync();

        // 1. 验证码校验（一次性消耗，无论成败）
        if (config.CaptchaEnabled && !await _securityService.VerifyCaptchaAsync(dto.CaptchaKey, dto.CaptchaCode, config))
        {
            await _securityService.RecordLoginFailureAsync(null, dto.UserName, "验证码错误", clientIp, userAgent);
            return ApiResponse<LoginResultDto>.Fail("验证码错误或已过期，请刷新后重试", 400);
        }

        // 2. 查询用户
        var user = await _userRepository.GetByUserNameAsync(dto.UserName);
        if (user == null)
        {
            // 账号不存在和密码错误返回相同提示，防止被枚举账号
            await _securityService.RecordLoginFailureAsync(null, dto.UserName, "用户名或密码错误", clientIp, userAgent);
            return ApiResponse<LoginResultDto>.Fail("用户名或密码错误", 401);
        }

        // 3. 账号状态校验
        if (user.Status != 1)
        {
            await _securityService.RecordLoginFailureAsync(user, dto.UserName, "账号已禁用", clientIp, userAgent);
            return ApiResponse<LoginResultDto>.Fail("账号已被禁用，请联系管理员", 403);
        }

        // 3.1 锁定状态校验（连续失败达到阈值后 lock_until 之内拒绝登录）
        var lockMinutes = await _securityService.GetLockRemainingMinutesAsync(user);
        if (lockMinutes != null)
        {
            await _securityService.RecordLoginFailureAsync(user, dto.UserName,
                "账号已锁定（连续登录失败），仍在锁定期内", clientIp, userAgent);
            return ApiResponse<LoginResultDto>.Fail($"密码连续错误次数过多，账号已锁定，请 {lockMinutes} 分钟后重试", 403);
        }

        // 4. BCrypt 校验密码（数据库存的是 BCrypt 密文）
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
        {
            await _securityService.RecordLoginFailureAsync(user, dto.UserName, "用户名或密码错误", clientIp, userAgent);
            return ApiResponse<LoginResultDto>.Fail("用户名或密码错误", 401);
        }

        // 5. 登录成功：清零失败计数、回写最后登录时间/IP、写成功登录日志
        user.LastLoginTime = DateTime.Now;
        user.LastLoginIp = clientIp;
        await _userRepository.SaveChangesAsync();
        await _securityService.RecordLoginSuccessAsync(user, clientIp, userAgent);

        // 6. 签发双令牌（access + refresh）
        var (accessToken, refreshToken, expiresIn, _) = _jwtService.GenerateTokenPair(user);

        // 7. 组装返回数据（不含密码；Token 为兼容旧前端的字段，与 AccessToken 同值）
        var result = new LoginResultDto
        {
            Token = accessToken,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            Id = user.Id,
            UserName = user.UserName,
            RealName = user.RealName,
            Avatar = user.Avatar,
            RoleId = user.RoleId,
            RoleName = user.Role?.RoleName,
            RoleCode = user.Role?.RoleCode,
            IsSuper = user.IsSuper
        };

        return ApiResponse<LoginResultDto>.Success(result, "登录成功");
    }
}
