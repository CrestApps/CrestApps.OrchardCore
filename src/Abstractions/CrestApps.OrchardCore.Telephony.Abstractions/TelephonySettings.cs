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

    /// <summary>
    /// Gets or sets the short codes this tenant may dial even though they are not international numbers, for
    /// example a carrier service code. Emergency codes are never dialable and cannot be opened through this list.
    /// </summary>
    public IList<string> AllowedShortCodes { get; set; } = [];
}
