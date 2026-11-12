namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// Whether the OSRM client may reach a server on the site's own network, read from the tenant's
/// <c>CrestApps:Geo:Osrm</c> configuration section, which only the host sets.
/// </summary>
public sealed class OsrmNetworkOptions
{
    /// <summary>
    /// The configuration section the options are read from.
    /// </summary>
    public const string SectionName = "CrestApps:Geo:Osrm";

    /// <summary>
    /// Gets or sets a value indicating whether a self-hosted OSRM server on a private or loopback address may be called.
    /// Link-local addresses, the cloud metadata service among them, are refused even then.
    /// </summary>
    public bool AllowPrivateNetwork { get; set; }
}
