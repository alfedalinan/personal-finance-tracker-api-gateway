using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Middleware;

public class OperationalLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperationalLoggingMiddleware> _logger;

    public OperationalLoggingMiddleware(RequestDelegate next, ILogger<OperationalLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var correlationId = context.Items["CorrelationId"]?.ToString() 
            ?? context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].FirstOrDefault() 
            ?? "unknown";

        string authFailureCategory = "None";

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;

            if (statusCode == StatusCodes.Status401Unauthorized)
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader))
                {
                    authFailureCategory = "MissingToken";
                }
                else if (authHeader.Contains("Expired", StringComparison.OrdinalIgnoreCase))
                {
                    authFailureCategory = "TokenExpired";
                }
                else
                {
                    authFailureCategory = "InvalidSignature";
                }
            }
            else if (statusCode == StatusCodes.Status403Forbidden)
            {
                authFailureCategory = "MissingScope";
            }

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | ClientIP: {ClientIp} | CorrelationId: {CorrelationId} | AuthFailure: {AuthFailureCategory}",
                method, path, statusCode, sw.ElapsedMilliseconds, clientIp, correlationId, authFailureCategory);
        }
    }
}
