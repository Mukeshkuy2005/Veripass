using Microsoft.AspNetCore.Authorization;

namespace VeriPass.API.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permissions = context.User.FindAll("permission").Select(c => c.Value);
        var roles = context.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);

        // System Admin bypasses granular permission requirement
        if (roles.Contains("ADMIN") || permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
