using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Authorization;

public class AuthorizationOptions
{
    public const string SectionName = "Authorization";

    public Dictionary<string, PolicyConfig> Policies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public class PolicyConfig
    {
        public List<string> Scopes { get; set; } = new();
        public List<string> Roles { get; set; } = new();
    }
}
