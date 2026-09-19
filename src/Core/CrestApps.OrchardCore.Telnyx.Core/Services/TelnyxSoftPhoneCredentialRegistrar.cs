using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules;
using CrestApps.Core.Telephony;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records which Telnyx browser SIP credential a user's soft phone is registered on, so the platform delivers
/// a call to a credential that can actually receive it.
/// </summary>
public sealed class TelnyxSoftPhoneCredentialRegistrar : ISoftPhoneCredentialRegistrar
{
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSoftPhoneCredentialRegistrar"/> class.
    /// </summary>
    public TelnyxSoftPhoneCredentialRegistrar(ITelnyxAgentCredentialStore credentialStore, TimeProvider timeProvider)
    {
        _credentialStore = credentialStore;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string ProviderName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public Task<bool> ReportRegisteredAsync(string userId, string credentialId, CancellationToken cancellationToken = default)
        => _credentialStore.MarkRegisteredAsync(userId, credentialId, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
