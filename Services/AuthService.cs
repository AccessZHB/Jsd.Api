using Jsd.Api.Entities;
using Jsd.Api.Models.Auth;
using Jsd.Api.Models.Common;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly ISysUserRepository _userRepository;
    private readonly JwtService _jwtService;

    public AuthService(ISysUserRepository userRepository, JwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// 登录流程：
    /// 1. 按账号查用户（带出角色）
    /// 2. 校验账号状态
    /// 3. BCrypt 校验密码
    /// 4. 回写最后登录时间/IP
    /// 5. 生成 JWT 并返回用户信息
    /// </summary>
    public async Task<ApiResponse<LoginResultDto>> LoginAsync(LoginDto dto, string? clientIp)
    {
        // 1. 查询用户
        var user = await _userRepository.GetByUserNameAsync(dto.UserName);
        if (user == null)
        {
            // 账号不存在和密码错误返回相同提示，防止被枚举账号
            return ApiResponse<LoginResultDto>.Fail("用户名或密码错误", 401);
        }

        // 2. 账号状态校验
        if (user.Status != 1)
        {
            return ApiResponse<LoginResultDto>.Fail("账号已被禁用，请联系管理员", 403);
        }

        // 3. BCrypt 校验密码（数据库存的是 BCrypt 密文）
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
        {
            return ApiResponse<LoginResultDto>.Fail("用户名或密码错误", 401);
        }

        // 4. 记录最后登录时间和IP
        user.LastLoginTime = DateTime.Now;
        user.LastLoginIp = clientIp;
        await _userRepository.SaveChangesAsync();

        // 5. 生成 Token
        var token = _jwtService.GenerateToken(user);

        // 6. 组装返回数据（不含密码）
        var result = new LoginResultDto
        {
            Token = token,
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
