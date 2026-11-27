using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configuration;

public class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Required(ErrorMessage = "Authority URL is required.")]
    [Url(ErrorMessage = "Authority must be a valid URL.")]
    public string Authority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Audience is required.")]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "ClientId is required.")]
    public string ClientId { get; set; } = string.Empty;

    public int BackchannelTimeoutSeconds { get; set; } = 30;

    public string? MetadataAddress { get; set; }

    [Range(0, 60, ErrorMessage = "Clock skew minutes must be between 0 and 60.")]
    public int ClockSkewMinutes { get; set; } = 5;

    public bool ValidateLifetime { get; set; } = true;

    public bool ValidateIssuer { get; set; } = true;

    public bool ValidateAudience { get; set; } = true;

    public bool ValidateIssuerSigningKey { get; set; } = true;

    public string? JwtSecret { get; set; }
}
