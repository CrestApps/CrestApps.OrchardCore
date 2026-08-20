using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Participates in the Contact Center feature lifecycle so in-flight Telnyx voice work is quiesced and
/// drained when the boundary that composes it is torn down or the tenant shuts down. The Telnyx Contact
/// Center voice adapter is now integration glue gated on the Telnyx provider and Contact Center Voice, so
/// one instance is registered per gating feature: disabling either one drains the shared work partition,
/// preserving the drain-on-disable behavior the former dedicated feature provided.
/// </summary>
internal sealed class TelnyxContactCenterFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public TelnyxContactCenterFeatureLifecycleParticipant(
        string featureId,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        FeatureId = featureId;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId { get; }

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(TelnyxConstants.ContactCenterVoiceWorkPartition);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
        => _workManager.DrainAsync(TelnyxConstants.ContactCenterVoiceWorkPartition, _drainTimeout, cancellationToken);
}
