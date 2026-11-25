using System.Security.Claims;
using System.Threading.RateLimiting;
using ApiGateway.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiGateway.RateLimiting;

public static class RateLimitingExtensions
{
    public const string StandardReadPolicy = "standard-read";
    public const string StrictWriteAuthPolicy = "strict-write-auth";

    public static IServiceCollection AddApiGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                int retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = (int)retryAfter.TotalSeconds;
                }

                context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Rate limit exceeded. Please try again later.",
                    retryAfterSeconds = retryAfterSeconds
                };

                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken);
            };

            // Standard Read Policy
            options.AddPolicy(StandardReadPolicy, httpContext =>
            {
                var rateLimitingOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.StandardRead.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitingOptions.StandardRead.WindowSeconds),
                        QueueLimit = rateLimitingOptions.StandardRead.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            // Strict Write/Auth Policy
            options.AddPolicy(StrictWriteAuthPolicy, httpContext =>
            {
                var rateLimitingOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.StrictWriteAuth.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitingOptions.StrictWriteAuth.WindowSeconds),
                        QueueLimit = rateLimitingOptions.StrictWriteAuth.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    public static string GetPartitionKey(HttpContext httpContext)
    {
        // 1. Authenticated subject claim ('sub' or 'nameidentifier')
        var subClaim = httpContext.User?.FindFirst("sub")?.Value
                       ?? httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(subClaim))
        {
            return $"user:{subClaim}";
        }

        // 2. Optional X-Client-Id header for unauthenticated/client-identified requests
        if (httpContext.Request.Headers.TryGetValue("X-Client-Id", out var clientId) && !string.IsNullOrEmpty(clientId))
        {
            return $"client:{clientId}";
        }

        // 3. Remote IP address fallback
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(remoteIp))
        {
            return $"ip:{remoteIp}";
        }

        return "ip:unknown";
    }
}
