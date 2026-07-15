using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.DialPad.Services;

internal sealed class DialPadContactCenterFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public DialPadContactCenterFeatureLifecycleParticipant(
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId => DialPadConstants.Feature.ContactCenterVoice;

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(FeatureId);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(FeatureId, _drainTimeout, cancellationToken);
    }

    public Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Activate(FeatureId);

        return Task.CompletedTask;
    }
}
