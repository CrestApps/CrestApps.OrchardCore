using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Participates in the Contact Center feature lifecycle so in-flight Asterisk voice or media work is quiesced
/// and drained when the boundary that composes it is torn down or the tenant shuts down. The Asterisk Contact
/// Center adapters are integration glue gated on the Asterisk provider and Contact Center Voice (or Voice
/// Media), so one instance is registered per gating feature over a shared work partition: disabling either
/// gating feature drains that partition, preserving the drain-on-disable behavior the former dedicated
/// features provided.
/// </summary>
internal sealed class AsteriskContactCenterFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly string _partitionKey;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public AsteriskContactCenterFeatureLifecycleParticipant(
        string featureId,
        string partitionKey,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        FeatureId = featureId;
        _partitionKey = partitionKey;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId { get; }

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(_partitionKey);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(_partitionKey, _drainTimeout, cancellationToken);
    }
}
