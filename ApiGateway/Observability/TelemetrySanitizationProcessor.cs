using System.Diagnostics;
using OpenTelemetry;

namespace ApiGateway.Observability;

public class TelemetrySanitizationProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "http.request.header.authorization",
        "http.request.header.cookie",
        "http.request.header.x-api-key",
        "authorization",
        "cookie",
        "password",
        "token",
        "secret",
        "api_key",
        "apikey"
    };

    public override void OnStart(Activity activity)
    {
        SanitizeActivity(activity);
        base.OnStart(activity);
    }

    public override void OnEnd(Activity activity)
    {
        SanitizeActivity(activity);
        base.OnEnd(activity);
    }

    private static void SanitizeActivity(Activity activity)
    {
        if (activity.TagObjects == null) return;

        foreach (var tag in activity.TagObjects.ToList())
        {
            if (IsSensitiveKey(tag.Key))
            {
                activity.SetTag(tag.Key, "[REDACTED]");
            }
            else if (tag.Value is string strVal && ContainsSensitiveContent(strVal))
            {
                activity.SetTag(tag.Key, "[REDACTED]");
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var sensitive in SensitiveKeys)
        {
            if (key.Contains(sensitive, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsSensitiveContent(string value)
    {
        return value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }
}
