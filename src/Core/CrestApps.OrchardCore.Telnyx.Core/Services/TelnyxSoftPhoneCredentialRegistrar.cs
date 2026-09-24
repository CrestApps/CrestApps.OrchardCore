using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records which Telnyx browser SIP credential a user's soft phone is registered on, so the platform delivers
/// a call to a credential that can actually receive it.
/// </summary>
public sealed class TelnyxSoftPhoneCredentialRegistrar : ISoftPhoneCredentialRegistrar, ISoftPhoneClientCapabilityRegistrar
{
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSoftPhoneCredentialRegistrar"/> class.
    /// </summary>
    public TelnyxSoftPhoneCredentialRegistrar(ITelnyxAgentCredentialStore credentialStore, IClock clock)
    {
        _credentialStore = credentialStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string ProviderName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public Task<bool> ReportRegisteredAsync(string userId, string credentialId, CancellationToken cancellationToken = default)
        => _credentialStore.MarkRegisteredAsync(userId, credentialId, _clock.UtcNow, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ReportCapabilitiesAsync(
        string userId,
        string credentialId,
        IReadOnlyCollection<string> capabilities,
        CancellationToken cancellationToken = default)
        => _credentialStore.SetClientCapabilitiesAsync(userId, credentialId, capabilities, cancellationToken);
}
