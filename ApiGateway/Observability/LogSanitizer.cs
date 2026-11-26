namespace ApiGateway.Observability;

public static class LogSanitizer
{
    public static string SanitizeHeader(string headerName, string headerValue)
    {
        if (string.IsNullOrEmpty(headerValue)) return headerValue;
        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
            headerName.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
            headerName.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase))
        {
            return "[REDACTED]";
        }
        return headerValue;
    }

    public static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (text.Contains("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("secret", StringComparison.OrdinalIgnoreCase))
        {
            return "[REDACTED]";
        }
        return text;
    }
}
