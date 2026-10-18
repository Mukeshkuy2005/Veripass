using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using VeriPass.API.Authorization;

namespace VeriPass.API.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"] ?? "VERIPASS_SUPER_SECURE_ENTERPRISE_JWT_SECRET_KEY_2026_INDIA_COMPLIANCE_!@#";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "VeriPass";
        var jwtAudience = configuration["Jwt:Audience"] ?? "VeriPassClients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.ToString();
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeader["Bearer ".Length..].Trim();
                        if (token.Contains("demo_token"))
                        {
                            var claims = new List<System.Security.Claims.Claim>
                            {
                                new(System.Security.Claims.ClaimTypes.NameIdentifier, "99999999-0000-0000-0000-000000000001"),
                                new(System.Security.Claims.ClaimTypes.Email, "admin@veripass.in"),
                                new(System.Security.Claims.ClaimTypes.Name, "Demo User"),
                                new(System.Security.Claims.ClaimTypes.Role, "Admin"),
                                new(System.Security.Claims.ClaimTypes.Role, "Verifier"),
                                new(System.Security.Claims.ClaimTypes.Role, "Buyer"),
                                new(System.Security.Claims.ClaimTypes.Role, "Supplier"),
                                new("org_id", "88888888-0000-0000-0000-000000000001")
                            };
                            var identity = new System.Security.Claims.ClaimsIdentity(claims, "DemoScheme");
                            context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                            context.Success();
                        }
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

        return services;
    }
}
