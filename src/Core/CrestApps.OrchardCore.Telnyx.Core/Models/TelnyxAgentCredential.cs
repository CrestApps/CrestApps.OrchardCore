namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// Durable, per-tenant record of a browser SIP credential minted from Telnyx for an authenticated user's
/// soft phone. It maps the authenticated user to the Telnyx telephony credential and its SIP username so
/// the Contact Center voice provider can resolve the agent's live SIP endpoint when bridging a call, and so
/// the credential can be revoked at Telnyx on sign-out.
/// </summary>
public sealed class TelnyxAgentCredential
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant that owns the credential.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user the credential is bound to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx telephony credential identifier used to revoke the credential.
    /// </summary>
    public string CredentialId { get; set; }

    /// <summary>
    /// Gets or sets the SIP username the browser registers with. It also identifies the agent endpoint the
    /// Contact Center bridges to (<c>sip:{SipUsername}@{domain}</c>).
    /// </summary>
    public string SipUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential was issued.
    /// </summary>
    public DateTime IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the browser client reported that it completed SIP registration on this
    /// credential, when it has reported one.
    /// </summary>
    /// <remarks>
    /// Several credentials can be live for one user at once, and the client is registered on exactly one of
    /// them. This is what distinguishes it: a credential that was minted but whose registration never
    /// completed has no value here, and delivering a call to it is refused by Telnyx with SIP 486.
    /// </remarks>
    public DateTime? RegisteredUtc { get; set; }

    /// <summary>
    /// Gets or sets the soft-phone connection that reported registering on this credential, when it said which.
    /// </summary>
    /// <remarks>
    /// One agent can have the soft phone open in several windows, each registered on a credential of its own. The
    /// window that registered last used to be where every call went, so closing it left calls going to a credential
    /// nothing was listening on until another window happened to register again.
    /// </remarks>
    public string RegisteredConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the connection that registered on this credential closed, when it has, and nothing
    /// has registered on the credential since.
    /// </summary>
    /// <remarks>
    /// A closed connection is most likely a window that is gone, so a credential registered by a window still open is
    /// preferred over it. It is not revoked: the connection also closes when a window only loses its link to the
    /// server for a moment, and the window's registration with the provider outlives that.
    /// </remarks>
    public DateTime? ConnectionClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets what the client registered on this credential reported it can do (see
    /// <c>TelephonyConstants.SoftPhoneClientCapabilities</c>). Empty for a client that reported nothing, which is
    /// treated as able to do none of them.
    /// </summary>
    public IList<string> ClientCapabilities { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the credential was revoked, when it has been revoked.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }
}
