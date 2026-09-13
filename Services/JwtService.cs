using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jsd.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Jsd.Api.Services;

/// <summary>
/// JWT Token 生成服务
/// 配置来源：appsettings.json 的 "Jwt" 节
/// </summary>
public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
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
            new("role_id", user.RoleId?.ToString() ?? "0")       // 角色ID（自定义声明）
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
}
