using CrestApps.OrchardCore.Telephony;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Revokes the browser SIP credentials Telnyx minted for a user when they sign out or their soft-phone
/// session ends, deleting them at Telnyx instead of letting them linger until natural expiry.
/// </summary>
public sealed class TelnyxSoftPhoneCredentialRevoker : ISoftPhoneCredentialRevoker
{
    private readonly ITelnyxTelephonyCredentialIssuer _credentialIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSoftPhoneCredentialRevoker"/> class.
    /// </summary>
    public TelnyxSoftPhoneCredentialRevoker(ITelnyxTelephonyCredentialIssuer credentialIssuer)
    {
        _credentialIssuer = credentialIssuer;
    }

    /// <inheritdoc/>
    public string ProviderName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public Task<int> RevokeForUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
        => _credentialIssuer.RevokeForUserAsync(userId, reason, cancellationToken);
}
