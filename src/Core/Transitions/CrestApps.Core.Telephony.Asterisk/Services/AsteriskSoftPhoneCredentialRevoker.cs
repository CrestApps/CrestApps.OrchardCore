using CrestApps.Core.Telephony;

namespace CrestApps.Core.Telephony.Asterisk.Services;

public sealed class AsteriskSoftPhoneCredentialRevoker : ISoftPhoneCredentialRevoker
{
    private readonly IAsteriskPjsipCredentialIssuer _credentialIssuer;

    public AsteriskSoftPhoneCredentialRevoker(IAsteriskPjsipCredentialIssuer credentialIssuer)
    {
        _credentialIssuer = credentialIssuer;
    }

    public string ProviderName => AsteriskConstants.ProviderTechnicalName;

    public Task<int> RevokeForUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default)
        => _credentialIssuer.RevokeUserAsync(userId, reason, cancellationToken);
}
