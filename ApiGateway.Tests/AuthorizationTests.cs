using System.Security.Claims;
using ApiGateway.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiGateway.Tests;

public class AuthorizationTests
{
    [Fact]
    public void AuthorizationOptions_BindsPoliciesSuccessfully()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authorization:Policies:RequireFinanceRead:Scopes:0", "finance.read"},
            {"Authorization:Policies:RequireFinanceRead:Roles:0", "FinanceReader"},
            {"Authorization:Policies:RequireAdmin:Roles:0", "Admin"}
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<ApiGateway.Authorization.AuthorizationOptions>()
            .Bind(configuration.GetSection(ApiGateway.Authorization.AuthorizationOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ApiGateway.Authorization.AuthorizationOptions>>().Value;

        Assert.NotNull(options);
        Assert.True(options.Policies.ContainsKey("RequireFinanceRead"));
        Assert.Equal("finance.read", options.Policies["RequireFinanceRead"].Scopes[0]);
        Assert.Equal("FinanceReader", options.Policies["RequireFinanceRead"].Roles[0]);
        Assert.True(options.Policies.ContainsKey("RequireAdmin"));
        Assert.Equal("Admin", options.Policies["RequireAdmin"].Roles[0]);
    }

    [Theory]
    // Scope matching
    [InlineData("finance.read", "finance.read", null, true)]
    [InlineData("finance.read finance.write", "finance.read", null, true)]
    [InlineData("finance.write", "finance.read", null, false)]
    [InlineData("finance.read", "finance.write", null, false)]
    // Scp claim matching
    [InlineData("finance.read", null, "finance.read", true)]
    // Role matching
    [InlineData(null, null, null, false, "Admin", "Admin", true)]
    [InlineData(null, null, null, false, "User", "Admin", false)]
    // Scope or Role matching when both specified
    [InlineData("finance.read", "finance.read", null, true, "User", "Admin", true)]
    [InlineData("finance.read", "finance.write", null, true, "Admin", "Admin", true)]
    [InlineData("finance.write", "finance.read", null, false, "User", "Admin", false)]
    public async Task ScopeHandler_EvaluatesScopesAndRolesCorrectly(
        string? scopeClaimValue,
        string? requiredScope,
        string? scpClaimValue,
        bool expectScopeSuccess,
        string? userRole = null,
        string? requiredRole = null,
        bool? expectedOverallSuccess = null)
    {
        var requiredScopes = !string.IsNullOrEmpty(requiredScope) ? new[] { requiredScope } : Array.Empty<string>();
        var requiredRoles = !string.IsNullOrEmpty(requiredRole) ? new[] { requiredRole } : Array.Empty<string>();

        var requirement = new ScopeRequirement(requiredScopes, requiredRoles);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "test-user")
        };

        if (!string.IsNullOrEmpty(scopeClaimValue))
        {
            claims.Add(new Claim("scope", scopeClaimValue));
        }

        if (!string.IsNullOrEmpty(scpClaimValue))
        {
            claims.Add(new Claim("scp", scpClaimValue));
        }

        if (!string.IsNullOrEmpty(userRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var handler = new ScopeHandler();
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        // Act
        await handler.HandleAsync(context);

        var shouldSucceed = expectedOverallSuccess ?? expectScopeSuccess;
        Assert.Equal(shouldSucceed, context.HasSucceeded);
    }

    [Fact]
    public async Task AuthorizationService_EvaluatesRegisteredPoliciesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireFinanceRead", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ScopeRequirement(new[] { "finance.read" }, new[] { "FinanceReader", "Admin" }));
            });
        });
        services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        // User with correct scope
        var userWithScope = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "user1"),
            new Claim("scope", "finance.read")
        }, "Test"));

        var result1 = await authService.AuthorizeAsync(userWithScope, "RequireFinanceRead");
        Assert.True(result1.Succeeded);

        // User with correct role
        var userWithRole = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "user2"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "Test"));

        var result2 = await authService.AuthorizeAsync(userWithRole, "RequireFinanceRead");
        Assert.True(result2.Succeeded);

        // User without scope or role
        var unauthorizedUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "user3"),
            new Claim("scope", "finance.write")
        }, "Test"));

        var result3 = await authService.AuthorizeAsync(unauthorizedUser, "RequireFinanceRead");
        Assert.False(result3.Succeeded);
    }
}
