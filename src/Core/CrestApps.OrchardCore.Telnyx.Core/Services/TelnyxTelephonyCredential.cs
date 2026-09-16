namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Represents a browser SIP credential minted from Telnyx for a soft-phone session.
/// </summary>
public sealed class TelnyxTelephonyCredential
{
    /// <summary>
    /// Gets or sets the Telnyx telephony credential identifier.
    /// </summary>
    public string CredentialId { get; set; }

    /// <summary>
    /// Gets or sets the SIP username the browser registers with.
    /// </summary>
    public string SipUsername { get; set; }

    /// <summary>
    /// Gets or sets the SIP password the browser registers with.
    /// </summary>
    public string SipPassword { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential expires.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
