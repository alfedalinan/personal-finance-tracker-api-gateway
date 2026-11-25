using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Middleware;

public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string TraceParentHeaderName = "traceparent";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeaderName] = correlationId;
        }

        var traceParent = context.Request.Headers[TraceParentHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            if (Activity.Current != null && !string.IsNullOrEmpty(Activity.Current.Id))
            {
                traceParent = Activity.Current.Id;
            }
            else
            {
                var traceId = Guid.NewGuid().ToString("N");
                var spanId = Guid.NewGuid().ToString("N")[..16];
                traceParent = $"00-{traceId}-{spanId}-01";
            }
            context.Request.Headers[TraceParentHeaderName] = traceParent;
        }

        context.Items["CorrelationId"] = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }

            if (!context.Response.Headers.ContainsKey(TraceParentHeaderName))
            {
                context.Response.Headers[TraceParentHeaderName] = traceParent;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
