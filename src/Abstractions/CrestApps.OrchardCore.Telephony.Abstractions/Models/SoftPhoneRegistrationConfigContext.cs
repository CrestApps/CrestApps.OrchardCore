namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the current browser soft-phone registration request.
/// </summary>
public sealed class SoftPhoneRegistrationConfigContext
{
    /// <summary>
    /// Gets or sets the technical provider name selected for the tenant.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the current user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name to present in SIP signaling.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional interaction identifier associated with this media session. This value is
    /// non-authoritative metadata only: it must never be used to authorize credential issuance or to
    /// derive the server-owned media session identity, because it can be supplied by the caller.
    /// </summary>
    public string InteractionId { get; set; }
}
