using System.Diagnostics;
using System.Net;
using ApiGateway.Middleware;
using ApiGateway.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace ApiGateway.Tests;

public class ObservabilityTests
{
    [Fact]
    public void LogSanitizer_MasksSensitiveHeadersAndSecrets()
    {
        var sanitizedAuth = LogSanitizer.SanitizeHeader("Authorization", "Bearer secret-token-123");
        var sanitizedCookie = LogSanitizer.SanitizeHeader("Cookie", "session=xyz");
        var sanitizedRegular = LogSanitizer.SanitizeHeader("X-Custom", "normal-value");
        var sanitizedText = LogSanitizer.SanitizeText("Password is mysecretpassword");
        var normalText = LogSanitizer.SanitizeText("Hello world");

        Assert.Equal("[REDACTED]", sanitizedAuth);
        Assert.Equal("[REDACTED]", sanitizedCookie);
        Assert.Equal("normal-value", sanitizedRegular);
        Assert.Equal("[REDACTED]", sanitizedText);
        Assert.Equal("Hello world", normalText);
    }

    [Fact]
    public void TelemetrySanitizationProcessor_RedactsSensitiveActivityTags()
    {
        var activitySource = new ActivitySource("ApiGateway.Test");
        Activity? capturedActivity = null;

        var processor = new TelemetrySanitizationProcessor();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { },
            ActivityStopped = activity => { capturedActivity = activity; }
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = activitySource.StartActivity("TestActivity"))
        {
            activity?.SetTag("http.request.header.authorization", "Bearer secret-jwt");
            activity?.SetTag("normal.tag", "safe-value");
            activity?.Stop();
        }

        Assert.NotNull(capturedActivity);
        // Run processor on end
        processor.OnEnd(capturedActivity!);

        Assert.Equal("[REDACTED]", capturedActivity!.GetTagItem("http.request.header.authorization"));
        Assert.Equal("safe-value", capturedActivity.GetTagItem("normal.tag"));
    }

    [Fact]
    public void GatewayHeadSampler_SamplesAuthAndMutations100Percent()
    {
        var sampler = new GatewayHeadSampler(0.0); // 0% ratio for reads

        var authParams = new SamplingParameters(
            default,
            default,
            "GET /auth/login",
            ActivityKind.Server,
            new KeyValuePair<string, object?>[] { new("http.route", "/auth/login") },
            null);

        var resultAuth = sampler.ShouldSample(in authParams);
        Assert.Equal(SamplingDecision.RecordAndSample, resultAuth.Decision);

        var postParams = new SamplingParameters(
            default,
            default,
            "POST /api/transactions",
            ActivityKind.Server,
            new KeyValuePair<string, object?>[] { new("http.method", "POST") },
            null);

        var resultPost = sampler.ShouldSample(in postParams);
        Assert.Equal(SamplingDecision.RecordAndSample, resultPost.Decision);
    }

    [Fact]
    public async Task OperationalLoggingMiddleware_LogsRequestSuccessfully()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.UseMiddleware<OperationalLoggingMiddleware>();
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/unauthorized")
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        }
                        else if (context.Request.Path == "/forbidden")
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status200OK;
                        }
                        await context.Response.WriteAsync("OK");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        
        var res1 = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        var res2 = await client.GetAsync("/unauthorized");
        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);

        var res3 = await client.GetAsync("/forbidden");
        Assert.Equal(HttpStatusCode.Forbidden, res3.StatusCode);
    }
}
