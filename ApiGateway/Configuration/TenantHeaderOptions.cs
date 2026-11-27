namespace ApiGateway.Configuration;

public class TenantHeaderOptions
{
    public const string SectionName = "TenantHeader";

    /// <summary>
    /// Ordered list of claim names to check for tenant identification.
    /// Default priority: tenantId, tenant_id, tid.
    /// </summary>
    public List<string> ClaimFallbacks { get; set; } = new() { "tenantId", "tenant_id", "tid" };

    /// <summary>
    /// Name of the header to inject into downstream requests. Default is X-Tenant-ID.
    /// </summary>
    public string HeaderName { get; set; } = "X-Tenant-ID";
}
