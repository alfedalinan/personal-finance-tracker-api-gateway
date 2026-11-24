using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configuration;

public class DownstreamServicesOptions
{
    public const string SectionName = "DownstreamServices";

    [Required(ErrorMessage = "UserService URL is required.")]
    [Url(ErrorMessage = "UserService must be a valid URL.")]
    public string UserService { get; set; } = string.Empty;

    [Required(ErrorMessage = "TransactionService URL is required.")]
    [Url(ErrorMessage = "TransactionService must be a valid URL.")]
    public string TransactionService { get; set; } = string.Empty;
}
