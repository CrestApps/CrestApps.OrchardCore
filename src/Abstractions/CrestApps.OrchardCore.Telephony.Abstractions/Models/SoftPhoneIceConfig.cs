namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes ICE server configuration for browser media negotiation.
/// </summary>
public sealed class SoftPhoneIceConfig
{
    /// <summary>
    /// Gets or sets the ICE servers available to the browser.
    /// </summary>
    public IList<SoftPhoneIceServerConfig> IceServers { get; set; } = [];

    /// <summary>
    /// Gets or sets the ICE transport policy.
    /// </summary>
    public string IceTransportPolicy { get; set; }
}
