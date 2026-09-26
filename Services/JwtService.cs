using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jsd.Api.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Jsd.Api.Services;

/// <summary>
/// JWT Token 生成服务
/// 配置来源：appsettings.json 的 "Jwt" 节
/// 系统管理模块扩展：双令牌（access + refresh）+ 密码版本声明 ver（改密强制下线）
/// </summary>
public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public JwtService(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    /// <summary>
    /// 根据用户信息生成 JWT Token
    /// </summary>
    public string GenerateToken(SysUser user)
    {
        // 读取 Jwt 配置节（键名与 appsettings.json 保持一致）
        var section = _configuration.GetSection("Jwt");
        var secretKey = section["SecretKey"]!;
        var issuer = section["Issuer"];
        var audience = section["Audience"];
        var expireMinutes = int.Parse(section["ExpireMinutes"] ?? "720");

        // 对称密钥 + 签名算法
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 写入 Token 的声明（Claims）：后续接口通过这些声明识别当前用户
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),   // 用户ID
            new(ClaimTypes.Name, user.UserName),                 // 登录账号
            new(ClaimTypes.Role, user.Role?.RoleCode ?? string.Empty), // 角色编码
            new("is_super", user.IsSuper.ToString()),            // 是否超管（自定义声明）
            new("role_id", user.RoleId?.ToString() ?? "0"),      // 角色ID（自定义声明）
            new("ver", user.PwdVersion.ToString())               // 密码版本号（改密后旧 Token 全部失效）
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 生成双令牌（access + refresh）—— 系统管理模块登录/刷新用
    /// refresh 令牌带 jti 与 typ=refresh 声明；jti 存入内存缓存（TTL=refresh有效期），
    /// 刷新时核销（用过即删）实现防重放：旧 refreshToken 使用后立即失效。
    /// 返回：(access, refresh, access有效期秒, refresh的jti)
    /// </summary>
    public (string AccessToken, string RefreshToken, int ExpiresIn, string RefreshJti) GenerateTokenPair(SysUser user)
    {
        var section = _configuration.GetSection("Jwt");
        var secretKey = section["SecretKey"]!;
        var issuer = section["Issuer"];
        var audience = section["Audience"];
        var accessMinutes = int.Parse(section["ExpireMinutes"] ?? "720");
        // refresh 有效期：默认 7 天（文档 6.3），可在 appsettings Jwt:RefreshExpireMinutes 覆盖
        var refreshMinutes = int.Parse(section["RefreshExpireMinutes"] ?? "10080");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // ---------- access token：与 GenerateToken 一致的声明 ----------
        var accessClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role?.RoleCode ?? string.Empty),
            new("is_super", user.IsSuper.ToString()),
            new("role_id", user.RoleId?.ToString() ?? "0"),
            new("ver", user.PwdVersion.ToString())
        };
        var access = new JwtSecurityToken(
            issuer: issuer, audience: audience, claims: accessClaims,
            expires: DateTime.UtcNow.AddMinutes(accessMinutes), signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(access);

        // ---------- refresh token：只带身份 + jti + typ=refresh ----------
        var refreshJti = Guid.NewGuid().ToString("N");
        var refreshClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, refreshJti),
            new("typ", "refresh")
        };
        var refresh = new JwtSecurityToken(
            issuer: issuer, audience: audience, claims: refreshClaims,
            expires: DateTime.UtcNow.AddMinutes(refreshMinutes), signingCredentials: credentials);
        var refreshToken = new JwtSecurityTokenHandler().WriteToken(refresh);

        // jti 入缓存：存在=已签发且未被使用；刷新时取出并核销（防重放）
        _cache.Set($"refresh:jti:{refreshJti}", user.Id,
            TimeSpan.FromMinutes(refreshMinutes));

        return (accessToken, refreshToken, accessMinutes * 60, refreshJti);
    }

    /// <summary>
    /// 校验并核销 refresh token（一次性消耗，防重放）。
    /// 返回：null=无效/已使用/过期；否则为用户ID。
    /// </summary>
    public long? ConsumeRefreshToken(string refreshToken)
    {
        var section = _configuration.GetSection("Jwt");
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = section["Issuer"],
            ValidAudience = section["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["SecretKey"]!))
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(refreshToken, parameters, out _);

            // 必须是 refresh 类型的令牌（防止拿 access token 来刷新）
            if (principal.FindFirst("typ")?.Value != "refresh") return null;

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jti)) return null;

            // 取出并立即删除（一次性消耗：旧 refreshToken 用过即失效）
            if (!_cache.TryGetValue($"refresh:jti:{jti}", out long userId)) return null;
            _cache.Remove($"refresh:jti:{jti}");
            return userId;
        }
        catch
        {
            // 签名错误 / 过期 / 格式非法：一律要求重新登录
            return null;
        }
    }
}
