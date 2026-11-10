namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// What a platform and the store agreed: the version and the capabilities both offer.
/// </summary>
public sealed class UcpNegotiation
{
    /// <summary>
    /// Gets or sets the agreed version, or <see langword="null"/> when there is none.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the agreed capabilities, by name, with their versions.
    /// </summary>
    public IDictionary<string, string> Capabilities { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets which capabilities extend which, for the agreed ones.
    /// </summary>
    public IDictionary<string, IList<string>> Extends { get; set; } = new Dictionary<string, IList<string>>(StringComparer.Ordinal);
}
