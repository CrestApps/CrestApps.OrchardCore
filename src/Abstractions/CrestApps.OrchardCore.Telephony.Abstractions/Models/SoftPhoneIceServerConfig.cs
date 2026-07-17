namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes a single ICE server entry.
/// </summary>
public sealed class SoftPhoneIceServerConfig
{
    /// <summary>
    /// Gets or sets the STUN or TURN URLs for this server.
    /// </summary>
    public IList<string> Urls { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional time-limited TURN user name.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the optional time-limited TURN credential.
    /// </summary>
    public string Credential { get; set; }
}
