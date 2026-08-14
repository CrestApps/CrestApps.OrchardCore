namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Stores the current user's telephony provider tokens, keyed by the provider technical name.
/// The token values held by the entries are encrypted at rest.
/// </summary>
public sealed class TelephonyUserConnections
{
    /// <summary>
    /// Gets or sets the map of provider technical name to the user's encrypted tokens for that provider.
    /// </summary>
    public Dictionary<string, TelephonyUserTokens> Connections { get; set; } = [];
}
