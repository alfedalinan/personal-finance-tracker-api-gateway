using ApiGateway.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ApiGateway.Middleware;

public class TenantIdInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantHeaderOptions _options;

    public TenantIdInjectionMiddleware(RequestDelegate next, IOptions<TenantHeaderOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Check if the request path starts with /api/
        var path = context.Request.Path;
        var isApiRoute = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

        if (isApiRoute)
        {
            // 2. Prevent tenant spoofing by unconditionally removing any client-supplied X-Tenant-ID header
            context.Request.Headers.Remove(_options.HeaderName);

            // 3. Check if user is authenticated
            var user = context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                string? tenantId = null;

                // Search claims in priority order specified by ClaimFallbacks
                foreach (var claimName in _options.ClaimFallbacks)
                {
                    var claim = user.FindFirst(c => string.Equals(c.Type, claimName, StringComparison.OrdinalIgnoreCase));
                    if (claim != null && !string.IsNullOrWhiteSpace(claim.Value))
                    {
                        tenantId = claim.Value.Trim();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(tenantId))
                {
                    // Inject tenant ID header for downstream services
                    context.Request.Headers[_options.HeaderName] = tenantId;
                }
                else
                {
                    // Authenticated request to /api/ missing a valid tenant claim on a protected route requiring tenant context.
                    // Return 400 Bad Request with a JSON error payload consistent with gateway error handling.
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";

                    var correlationId = context.Items["CorrelationId"]?.ToString()
                        ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString();

                    var errorResponse = new
                    {
                        error = "Bad Request",
                        message = "Authenticated request is missing a valid tenant identification claim.",
                        correlationId = correlationId,
                        statusCode = 400
                    };

                    await context.Response.WriteAsJsonAsync(errorResponse);
                    return;
                }
            }
            // For unauthenticated / anonymous routes under /api/, do not inject X-Tenant-ID and proceed normally.
        }

        await _next(context);
    }
}
