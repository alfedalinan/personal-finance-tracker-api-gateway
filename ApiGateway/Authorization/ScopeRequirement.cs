using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.Authorization;

public class ScopeRequirement : IAuthorizationRequirement
{
    public IEnumerable<string> RequiredScopes { get; }
    public IEnumerable<string> RequiredRoles { get; }

    public ScopeRequirement(IEnumerable<string>? requiredScopes = null, IEnumerable<string>? requiredRoles = null)
    {
        RequiredScopes = requiredScopes ?? Enumerable.Empty<string>();
        RequiredRoles = requiredRoles ?? Enumerable.Empty<string>();
    }

    public ScopeRequirement(string requiredScope)
    {
        RequiredScopes = new[] { requiredScope };
        RequiredRoles = Enumerable.Empty<string>();
    }
}

public class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var user = context.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        bool scopeSatisfied = !requirement.RequiredScopes.Any() || requirement.RequiredScopes.All(requiredScope =>
            user.Claims.Where(c => 
                c.Type.Equals("scope", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("scp", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("http://schemas.microsoft.com/identity/claims/scope", StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Any(userScope => string.Equals(userScope, requiredScope, StringComparison.OrdinalIgnoreCase))
        );

        bool roleSatisfied = !requirement.RequiredRoles.Any() || requirement.RequiredRoles.Any(requiredRole =>
            user.IsInRole(requiredRole) ||
            user.Claims.Any(c => 
                (c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
                 c.Type.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                 c.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(c.Value, requiredRole, StringComparison.OrdinalIgnoreCase))
        );

        bool satisfied = false;
        if (requirement.RequiredScopes.Any() && requirement.RequiredRoles.Any())
        {
            satisfied = scopeSatisfied || roleSatisfied;
        }
        else if (requirement.RequiredScopes.Any())
        {
            satisfied = scopeSatisfied;
        }
        else if (requirement.RequiredRoles.Any())
        {
            satisfied = roleSatisfied;
        }
        else
        {
            satisfied = true;
        }

        if (satisfied)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
