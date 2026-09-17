namespace CrestApps.Core.Hosting;

/// <summary>
/// Names the tenant the current work belongs to, so keys, groups and correlation identifiers can be
/// scoped without the suite knowing how the host does multi-tenancy.
/// </summary>
public interface ITenantAccessor
{
    /// <summary>
    /// Gets the name of the current tenant.
    /// </summary>
    string TenantName { get; }
}
