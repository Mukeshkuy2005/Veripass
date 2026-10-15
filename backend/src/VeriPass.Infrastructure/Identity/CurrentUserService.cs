using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(idClaim, out var id)) return id;
            if (_httpContextAccessor.HttpContext?.Request?.Headers.TryGetValue("X-User-Id", out var hVal) == true &&
                Guid.TryParse(hVal, out var hId))
            {
                return hId;
            }
            return null;
        }
    }

    public Guid? OrganizationId
    {
        get
        {
            var orgClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("org_id")?.Value;
            if (Guid.TryParse(orgClaim, out var id)) return id;
            if (_httpContextAccessor.HttpContext?.Request?.Headers.TryGetValue("X-Organization-Id", out var headerVal) == true &&
                Guid.TryParse(headerVal, out var headerId))
            {
                return headerId;
            }
            return null;
        }
    }

    public string? UserEmail =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        ?? new List<string>();

    public IReadOnlyList<string> Permissions =>
        _httpContextAccessor.HttpContext?.User?.FindAll("permission").Select(c => c.Value).ToList()
        ?? new List<string>();

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
