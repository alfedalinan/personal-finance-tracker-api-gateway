using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiGateway.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly HttpMessageHandler _downstreamHandler;

    public CustomWebApplicationFactory(HttpMessageHandler downstreamHandler)
    {
        _downstreamHandler = downstreamHandler;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace or configure HTTP client factory or message handler if needed,
            // or configure test services.
        });
    }
}

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RootEndpoint_ReturnsHealthyMessage()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Personal Finance Tracker API Gateway is running!", content);
    }

    [Fact]
    public async Task GatewaySanitizesServerHeadersOnProxyResponse()
    {
        var client = _factory.CreateClient();
        // Even on 404 or root or downstream responses, ensure Server and X-Powered-By are not present
        var response = await client.GetAsync("/");

        Assert.False(response.Headers.Contains("Server"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
    }

    [Fact]
    public async Task GatewayLatency_OverheadUnder50Ms()
    {
        var client = _factory.CreateClient();
        
        // Warm up
        await client.GetAsync("/");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 100;
        
        for (int i = 0; i < iterations; i++)
        {
            var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        
        stopwatch.Stop();
        var averageMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert average gateway overhead is well below 50ms (typically <5ms in TestServer)
        Assert.True(averageMs < 50.0, $"Average request latency {averageMs}ms exceeded 50ms budget.");
    }
}
