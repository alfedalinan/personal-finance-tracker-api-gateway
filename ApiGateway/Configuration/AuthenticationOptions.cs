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
}
