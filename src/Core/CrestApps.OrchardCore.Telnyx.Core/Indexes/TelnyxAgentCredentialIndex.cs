using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telnyx.Indexes;

/// <summary>
/// YesSql index used to query durable Telnyx browser SIP credentials by owning user, SIP username, Telnyx
/// credential id, expiry, and revocation state.
/// </summary>
public sealed class TelnyxAgentCredentialIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the name of the tenant that owns the credential.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user the credential is bound to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx telephony credential identifier.
    /// </summary>
    public string CredentialId { get; set; }

    /// <summary>
    /// Gets or sets the SIP username the browser registers with.
    /// </summary>
    public string SipUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the credential has been revoked.
    /// </summary>
    public bool Revoked { get; set; }
}
