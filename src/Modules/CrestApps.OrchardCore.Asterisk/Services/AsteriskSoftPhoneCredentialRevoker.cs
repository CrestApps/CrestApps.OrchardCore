using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Telemetry;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskSoftPhoneCredentialRevoker : ISoftPhoneCredentialRevoker
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
        => TelephonyTelemetry.MeasureSoftPhoneCredentialAsync(
            ProviderName,
            TelephonyTelemetry.CredentialRevoke,
            () => _credentialIssuer.RevokeUserAsync(userId, reason, cancellationToken));
}
