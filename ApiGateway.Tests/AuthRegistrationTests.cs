using System.Net;
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

public class AuthRegistrationTests
{
    [Fact]
    public void AuthRoute_Configuration_MapsCorrectlyAndAppliesStrictWriteAuthPolicy()
    {
        var configData = new Dictionary<string, string?>
        {
            {"ReverseProxy:Routes:auth-route:ClusterId", "identity-cluster"},
            {"ReverseProxy:Routes:auth-route:Match:Path", "/auth/{**catch-all}"},
            {"ReverseProxy:Routes:auth-route:Metadata:RateLimiterPolicy", "strict-write-auth"},
            {"ReverseProxy:Routes:auth-route:Transforms:0:PathRemovePrefix", "/auth"},
            {"ReverseProxy:Routes:auth-route:Transforms:1:PathPrefix", "/api/auth"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var routePath = configuration["ReverseProxy:Routes:auth-route:Match:Path"];
        var rateLimitPolicy = configuration["ReverseProxy:Routes:auth-route:Metadata:RateLimiterPolicy"];
        var removePrefix = configuration["ReverseProxy:Routes:auth-route:Transforms:0:PathRemovePrefix"];
        var addPrefix = configuration["ReverseProxy:Routes:auth-route:Transforms:1:PathPrefix"];

        Assert.Equal("/auth/{**catch-all}", routePath);
        Assert.Equal("strict-write-auth", rateLimitPolicy);
        Assert.Equal("/auth", removePrefix);
        Assert.Equal("/api/auth", addPrefix);
    }

    [Fact]
    public async Task AuthRegisterRoute_EnforcesStrictWriteAuthRateLimit()
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
        app.MapPost("/auth/register", () => Results.Created("/api/auth/users/1", new { id = 1, email = "test@example.com" }))
           .RequireRateLimiting(RateLimitingExtensions.StrictWriteAuthPolicy);

        await app.StartAsync();

        var server = app.GetTestServer();
        var client = server.CreateClient();

        // First registration request should succeed (201 Created)
        var content = new StringContent("{\"email\":\"test@example.com\",\"password\":\"Password123!\"}", System.Text.Encoding.UTF8, "application/json");
        var response1 = await client.PostAsync("/auth/register", content);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Second registration request should be rate-limited (429 Too Many Requests)
        var content2 = new StringContent("{\"email\":\"test2@example.com\",\"password\":\"Password123!\"}", System.Text.Encoding.UTF8, "application/json");
        var response2 = await client.PostAsync("/auth/register", content2);
        Assert.Equal((HttpStatusCode)429, response2.StatusCode);
        Assert.Equal("application/problem+json", response2.Content.Headers.ContentType?.MediaType);

        await app.StopAsync();
    }
}
