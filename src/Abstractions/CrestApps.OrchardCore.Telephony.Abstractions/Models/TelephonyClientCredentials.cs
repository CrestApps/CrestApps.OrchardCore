namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the bootstrap configuration a soft phone client needs to connect to a provider's
/// browser SDK. Providers that perform all call control server-side may return only their technical
/// name with an empty <see cref="Settings"/> collection.
/// </summary>
public sealed class TelephonyClientCredentials
{
    /// <summary>
    /// Gets or sets the technical name of the provider that issued the credentials.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets an optional short-lived access token for the provider's browser SDK.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the token expires, when applicable.
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets an optional collection of non-sensitive, provider-specific settings the client
    /// SDK needs in order to initialize.
    /// </summary>
    public IDictionary<string, string> Settings { get; set; }
}
