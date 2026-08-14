using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Dialpad.Services;

internal sealed class DialpadContactCenterFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public DialpadContactCenterFeatureLifecycleParticipant(
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId => DialpadConstants.Feature.ContactCenterVoice;

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(FeatureId);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(FeatureId, _drainTimeout, cancellationToken);
    }
}
