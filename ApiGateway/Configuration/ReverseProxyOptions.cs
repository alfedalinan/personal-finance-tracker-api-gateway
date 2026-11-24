using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configuration;

public class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds.")]
    public int TimeoutSeconds { get; set; } = 30;
}
