namespace CrestApps.OrchardCore.TenantHierarchy.Core;

/// <summary>
/// Holds the options of the guard that refuses outbound requests from parent and child tenants to internal addresses
/// and to the tenants of this application.
/// </summary>
public sealed class TenantHierarchyEgressOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the guard is on.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether loopback, link-local and private addresses are refused.
    /// </summary>
    public bool BlockPrivateNetworks { get; set; } = true;

    /// <summary>
    /// Gets or sets the hosts that are always allowed, for example a local AI model server.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];
}
