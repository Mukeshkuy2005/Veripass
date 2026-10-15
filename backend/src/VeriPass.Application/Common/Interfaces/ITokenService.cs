using VeriPass.Domain.Entities;

namespace VeriPass.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, Organization? currentOrg, Role? currentRole, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashToken(string token);
}
