using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiGateway.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ApiGateway.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesHeaderWhenMissing()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.Run(async context =>
                    {
                        var correlationId = context.Items["CorrelationId"]?.ToString();
                        await context.Response.WriteAsync(correlationId ?? "");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.CorrelationIdHeaderName));
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.TraceParentHeaderName));

        var correlationIdInHeader = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeaderName).FirstOrDefault();
        var traceParentInHeader = response.Headers.GetValues(CorrelationIdMiddleware.TraceParentHeaderName).FirstOrDefault();
        var body = await response.Content.ReadAsStringAsync();

        Assert.NotNull(correlationIdInHeader);
        Assert.NotEmpty(correlationIdInHeader);
        Assert.Equal(correlationIdInHeader, body);
        Assert.NotNull(traceParentInHeader);
        Assert.StartsWith("00-", traceParentInHeader);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_PropagatesExistingHeaders()
    {
        var customCorrelationId = "custom-corr-12345";
        var customTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.Run(async context =>
                    {
                        var correlationId = context.Items["CorrelationId"]?.ToString();
                        await context.Response.WriteAsync(correlationId ?? "");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeaderName, customCorrelationId);
        request.Headers.Add(CorrelationIdMiddleware.TraceParentHeaderName, customTraceParent);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.CorrelationIdHeaderName));
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.TraceParentHeaderName));

        var correlationIdInHeader = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeaderName).FirstOrDefault();
        var traceParentInHeader = response.Headers.GetValues(CorrelationIdMiddleware.TraceParentHeaderName).FirstOrDefault();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(customCorrelationId, correlationIdInHeader);
        Assert.Equal(customCorrelationId, body);
        Assert.Equal(customTraceParent, traceParentInHeader);
    }

    [Fact]
    public async Task GlobalExceptionHandler_ReturnsSanitizedJsonErrorResponse()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseExceptionHandler(exceptionHandlerApp =>
                    {
                        exceptionHandlerApp.Run(async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            context.Response.ContentType = "application/json";

                            var correlationId = context.Items["CorrelationId"]?.ToString()
                                ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                                ?? Guid.NewGuid().ToString();

                            var responseObj = new
                            {
                                error = "Internal Server Error",
                                message = "An unexpected error occurred processing your request.",
                                correlationId = correlationId,
                                statusCode = 500
                            };

                            await context.Response.WriteAsJsonAsync(responseObj);
                        });
                    });

                    app.UseMiddleware<CorrelationIdMiddleware>();

                    app.Run(context =>
                    {
                        throw new InvalidOperationException("Sensitive internal details stack trace C:\\secret\\file.cs");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var contentString = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sensitive internal details", contentString);
        Assert.DoesNotContain("stack trace", contentString);
        Assert.DoesNotContain("file.cs", contentString);

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("error"));
        Assert.True(json.ContainsKey("message"));
        Assert.True(json.ContainsKey("correlationId"));
        Assert.Equal("Internal Server Error", json["error"].ToString());
    }

    [Fact]
    public async Task MaxRequestBodySize_RejectsPayloadsExceedingLimit()
    {
        const long maxLimit = 100; // 100 bytes limit for test

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > maxLimit)
                        {
                            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                            context.Response.ContentType = "application/json";
                            var correlationId = context.Items["CorrelationId"]?.ToString();
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Payload Too Large",
                                message = $"Request body exceeds the maximum allowed size of {maxLimit} bytes.",
                                correlationId = correlationId,
                                statusCode = 413
                            });
                            return;
                        }
                        await next();
                    });

                    app.Run(async context =>
                    {
                        await context.Response.WriteAsync("OK");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Under limit request
        var smallContent = new StringContent("small body", Encoding.UTF8, "text/plain");
        var smallResponse = await client.PostAsync("/", smallContent);
        Assert.Equal(HttpStatusCode.OK, smallResponse.StatusCode);

        // Over limit request
        var largeString = new string('A', 200);
        var largeContent = new StringContent(largeString, Encoding.UTF8, "text/plain");
        var largeResponse = await client.PostAsync("/", largeContent);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, largeResponse.StatusCode);

        var json = await largeResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.Equal("Payload Too Large", json["error"].ToString());
    }

    [Fact]
    public async Task CorsPolicy_EnforcesAllowedOrigins()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddCors(options =>
                    {
                        options.AddDefaultPolicy(policy =>
                        {
                            policy.WithOrigins("https://allowed.example.com")
                                  .AllowAnyHeader()
                                  .AllowAnyMethod();
                        });
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseCors();
                    app.Run(async context =>
                    {
                        await context.Response.WriteAsync("OK");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Allowed origin
        var allowedRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        allowedRequest.Headers.Add("Origin", "https://allowed.example.com");
        var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.True(allowedResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("https://allowed.example.com", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());

        // Disallowed origin
        var disallowedRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        disallowedRequest.Headers.Add("Origin", "https://disallowed.example.com");
        var disallowedResponse = await client.SendAsync(disallowedRequest);

        Assert.Equal(HttpStatusCode.OK, disallowedResponse.StatusCode);
        Assert.False(disallowedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
