using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Participates in the Contact Center feature lifecycle so in-flight Dialpad voice work is quiesced and
/// drained when the boundary that composes it is torn down or the tenant shuts down. The Dialpad Contact
/// Center voice adapter is integration glue gated on the Dialpad provider and Contact Center Voice, so one
/// instance is registered per gating feature: disabling either one drains the shared work partition.
/// </summary>
internal sealed class DialpadContactCenterFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public DialpadContactCenterFeatureLifecycleParticipant(
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
        _workManager.Quiesce(DialpadConstants.ContactCenterVoiceWorkPartition);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(DialpadConstants.ContactCenterVoiceWorkPartition, _drainTimeout, cancellationToken);
    }
}
