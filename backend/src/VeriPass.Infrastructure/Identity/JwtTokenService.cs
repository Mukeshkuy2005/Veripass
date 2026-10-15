using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Identity;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user, Organization? currentOrg, Role? currentRole, IEnumerable<string> permissions)
    {
        var secret = _configuration["Jwt:Secret"] ?? "VERIPASS_SUPER_SECURE_JWT_SECRET_KEY_FOR_DEVELOPMENT_2026_!#@";
        var issuer = _configuration["Jwt:Issuer"] ?? "VeriPass";
        var audience = _configuration["Jwt:Audience"] ?? "VeriPassClients";
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var mins) ? mins : 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (currentOrg != null)
        {
            claims.Add(new Claim("org_id", currentOrg.Id.ToString()));
            claims.Add(new Claim("org_name", currentOrg.Name));
            claims.Add(new Claim("org_type", currentOrg.Type.ToString()));
        }

        if (currentRole != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, currentRole.Name));
        }

        foreach (var perm in permissions)
        {
            claims.Add(new Claim("permission", perm));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashed = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashed);
    }
}
