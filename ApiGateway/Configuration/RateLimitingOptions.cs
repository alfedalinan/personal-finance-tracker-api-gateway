using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configuration;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public RateLimitPolicyOptions StandardRead { get; set; } = new()
    {
        PermitLimit = 100,
        WindowSeconds = 60,
        QueueLimit = 0
    };

    public RateLimitPolicyOptions StrictWriteAuth { get; set; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60,
        QueueLimit = 0
    };
}

public class RateLimitPolicyOptions
{
    [Range(1, int.MaxValue, ErrorMessage = "Permit limit must be at least 1.")]
    public int PermitLimit { get; set; } = 100;

    [Range(1, int.MaxValue, ErrorMessage = "Window seconds must be at least 1.")]
    public int WindowSeconds { get; set; } = 60;

    [Range(0, int.MaxValue, ErrorMessage = "Queue limit cannot be negative.")]
    public int QueueLimit { get; set; } = 0;
}
