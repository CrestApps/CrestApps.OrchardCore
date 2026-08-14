namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Represents the telephony site settings shared by all providers.
/// </summary>
public sealed class TelephonySettings
{
    /// <summary>
    /// Gets or sets the technical name of the telephony provider used by default when no explicit
    /// provider is requested.
    /// </summary>
    public string DefaultProviderName { get; set; }
}
