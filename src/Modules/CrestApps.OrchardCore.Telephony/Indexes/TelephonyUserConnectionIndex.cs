using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Indexes;

/// <summary>
/// Indexes provider-specific connected-user account identifiers to the owning Orchard user.
/// </summary>
public sealed class TelephonyUserConnectionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user id that owns the provider connection.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the provider's stable user id for the connected account.
    /// </summary>
    public string RemoteUserId { get; set; }

    /// <summary>
    /// Gets or sets the normalized remote account email address.
    /// </summary>
    public string NormalizedRemoteUserEmail { get; set; }

    /// <summary>
    /// Gets or sets the normalized remote account phone number.
    /// </summary>
    public string NormalizedRemotePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the owning Orchard user is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
