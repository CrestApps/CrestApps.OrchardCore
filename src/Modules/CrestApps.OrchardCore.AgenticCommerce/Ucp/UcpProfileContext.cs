namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The store's UCP profile for a version, which features add their capabilities and transports to.
/// </summary>
public sealed class UcpProfileContext
{
    /// <summary>
    /// Gets or sets the version the profile is for.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the tenant's absolute base address.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets the capabilities and extensions offered.
    /// </summary>
    public IList<UcpCapability> Capabilities { get; } = [];

    /// <summary>
    /// Gets the transports the shopping service is served over.
    /// </summary>
    public IList<UcpService> Services { get; } = [];
}
