using System.Diagnostics;
using OpenTelemetry.Trace;

namespace ApiGateway.Observability;

public class GatewayHeadSampler : Sampler
{
    private readonly TraceIdRatioBasedSampler _ratioSampler;

    public GatewayHeadSampler(double successReadSamplingRatio = 0.1)
    {
        _ratioSampler = new TraceIdRatioBasedSampler(successReadSamplingRatio);
    }

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        if (parameters.ParentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        var name = parameters.Name ?? string.Empty;
        var tags = parameters.Tags;

        bool isWriteOrSensitive = false;
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "http.method" && tag.Value is string method)
                {
                    if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                        method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                        method.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
                        method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
                    {
                        isWriteOrSensitive = true;
                    }
                }
                if (tag.Key == "http.route" && tag.Value is string route)
                {
                    if (route.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                        route.Contains("login", StringComparison.OrdinalIgnoreCase))
                    {
                        isWriteOrSensitive = true;
                    }
                }
            }
        }

        if (name.Contains("auth", StringComparison.OrdinalIgnoreCase) || isWriteOrSensitive)
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        return _ratioSampler.ShouldSample(in parameters);
    }
}
