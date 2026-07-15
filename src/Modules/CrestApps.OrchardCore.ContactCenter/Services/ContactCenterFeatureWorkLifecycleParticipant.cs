using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureWorkLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public ContactCenterFeatureWorkLifecycleParticipant(
        string featureId,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);
        ArgumentNullException.ThrowIfNull(workManager);
        ArgumentNullException.ThrowIfNull(options);

        FeatureId = featureId;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId { get; }

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
