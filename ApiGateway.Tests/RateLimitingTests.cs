using System.Net;
using System.Security.Claims;
using ApiGateway.Configuration;
using ApiGateway.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiGateway.Tests;

public class RateLimitingTests
{
    [Fact]
    public void RateLimitingOptions_ValidConfiguration_BindsSuccessfully()
    {
        var configData = new Dictionary<string, string?>
        {
            {"RateLimiting:Enabled", "true"},
            {"RateLimiting:StandardRead:PermitLimit", "50"},
            {"RateLimiting:StandardRead:WindowSeconds", "30"},
            {"RateLimiting:StrictWriteAuth:PermitLimit", "5"},
            {"RateLimiting:StrictWriteAuth:WindowSeconds", "30"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal(50, options.StandardRead.PermitLimit);
        Assert.Equal(30, options.StandardRead.WindowSeconds);
        Assert.Equal(5, options.StrictWriteAuth.PermitLimit);
        Assert.Equal(30, options.StrictWriteAuth.WindowSeconds);
    }

    [Fact]
    public void GetPartitionKey_WithAuthenticatedUserSub_ReturnsUserPartition()
    {
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim("sub", "user-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var key = RateLimitingExtensions.GetPartitionKey(httpContext);

        Assert.Equal("user:user-123", key);
    }

    [Fact]
    public void GetPartitionKey_WithAuthenticatedUserNameIdentifier_ReturnsUserPartition()
    {
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-456") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var key = RateLimitingExtensions.GetPartitionKey(httpContext);

        Assert.Equal("user:user-456", key);
    }

    [Fact]
    public void GetPartitionKey_UnauthenticatedWithClientHeader_ReturnsClientPartition()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Client-Id"] = "client-xyz";

        var key = RateLimitingExtensions.GetPartitionKey(httpContext);

        Assert.Equal("client:client-xyz", key);
    }

    [Fact]
    public void GetPartitionKey_UnauthenticatedWithIpAddress_ReturnsIpPartition()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");

        var key = RateLimitingExtensions.GetPartitionKey(httpContext);

        Assert.Equal("ip:192.168.1.50", key);
    }

    [Fact]
    public void GetPartitionKey_NoInfo_ReturnsUnknownPartition()
    {
        var httpContext = new DefaultHttpContext();

        var key = RateLimitingExtensions.GetPartitionKey(httpContext);

        Assert.Equal("ip:unknown", key);
    }

    [Fact]
    public async Task RateLimiterMiddleware_ExceedingLimit_Returns429WithProblemDetails()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"RateLimiting:Enabled", "true"},
            {"RateLimiting:StrictWriteAuth:PermitLimit", "1"},
            {"RateLimiting:StrictWriteAuth:WindowSeconds", "60"}
        });

        builder.Services.AddApiGatewayRateLimiting(builder.Configuration);

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/test", () => Results.Ok("Success"))
           .RequireRateLimiting(RateLimitingExtensions.StrictWriteAuthPolicy);

        await app.StartAsync();

        var server = app.GetTestServer();
        
        // First request should succeed
        var response1 = await server.CreateClient().GetAsync("/test");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Second request should be rate-limited (429)
        var response2 = await server.CreateClient().GetAsync("/test");
        Assert.Equal((HttpStatusCode)429, response2.StatusCode);
        Assert.Equal("application/problem+json", response2.Content.Headers.ContentType?.MediaType);
        Assert.True(response2.Headers.Contains("Retry-After"));

        var content = await response2.Content.ReadAsStringAsync();
        Assert.Contains("Too Many Requests", content);
        Assert.Contains("Rate limit exceeded", content);

        await app.StopAsync();
    }
}
