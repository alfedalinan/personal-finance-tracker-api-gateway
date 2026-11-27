using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ApiGateway.Configuration;
using ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ApiGateway.Tests;

public class TenantIdInjectionMiddlewareTests
{
    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public static ClaimsPrincipal? CurrentPrincipal { get; set; }

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (CurrentPrincipal == null || !(CurrentPrincipal.Identity?.IsAuthenticated ?? false))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var ticket = new AuthenticationTicket(CurrentPrincipal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private async Task<IHost> BuildTestHostAsync(ClaimsPrincipal? principal, Action<IServiceCollection>? configureServices = null)
    {
        TestAuthHandler.CurrentPrincipal = principal;

        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAuthentication("TestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

                    services.AddAuthorization();
                    services.AddRouting();

                    services.Configure<TenantHeaderOptions>(options =>
                    {
                        options.HeaderName = "X-Tenant-ID";
                        options.ClaimFallbacks = new List<string> { "tenantId", "tenant_id", "tid" };
                    });

                    configureServices?.Invoke(services);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseMiddleware<TenantIdInjectionMiddleware>();
                    app.Run(async context =>
                    {
                        var tenantHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                        await context.Response.WriteAsync(tenantHeader ?? "NO_TENANT");
                    });
                });
            })
            .StartAsync();
    }

    [Theory]
    [InlineData("tenantId", "tenant-abc-123")]
    [InlineData("tenant_id", "tenant-def-456")]
    [InlineData("tid", "tenant-ghi-789")]
    public async Task InjectsTenantHeader_WhenValidClaimExists(string claimType, string claimValue)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(claimType, claimValue)
        }, "TestScheme");
        var principal = new ClaimsPrincipal(identity);

        using var host = await BuildTestHostAsync(principal);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(claimValue, body);
    }

    [Fact]
    public async Task TrimsWhitespace_FromExtractedTenantId()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("tenantId", "  tenant-spaced-999  ")
        }, "TestScheme");
        var principal = new ClaimsPrincipal(identity);

        using var host = await BuildTestHostAsync(principal);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("tenant-spaced-999", body);
    }

    [Fact]
    public async Task PreventsSpoofing_OverwritesClientSuppliedTenantHeader()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("tenantId", "authentic-tenant-id")
        }, "TestScheme");
        var principal = new ClaimsPrincipal(identity);

        using var host = await BuildTestHostAsync(principal);
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/resource");
        request.Headers.Add("X-Tenant-ID", "spoofed-tenant-id");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("authentic-tenant-id", body);
    }

    [Fact]
    public async Task ReturnsBadRequest_WhenAuthenticatedUserLacksTenantClaim()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "TestScheme");
        var principal = new ClaimsPrincipal(identity);

        using var host = await BuildTestHostAsync(principal);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/resource");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.Equal("Bad Request", json["error"].ToString());
        Assert.Contains("tenant identification claim", json["message"].ToString());
    }

    [Fact]
    public async Task ProceedsSuccessfully_WhenUnauthenticatedOnApiRoute()
    {
        using var host = await BuildTestHostAsync(null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/public-endpoint");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("NO_TENANT", body);
    }

    [Fact]
    public async Task IgnoresNonApiRoutes_WithoutAuthenticationOrTenantRequirements()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.Configure<TenantHeaderOptions>(options =>
                    {
                        options.HeaderName = "X-Tenant-ID";
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<TenantIdInjectionMiddleware>();
                    app.Run(async context =>
                    {
                        var tenantHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                        await context.Response.WriteAsync(tenantHeader ?? "NO_TENANT");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Tenant-ID", "client-supplied");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Non-api route should preserve client header or not require injection
        Assert.Equal("client-supplied", body);
    }
}
