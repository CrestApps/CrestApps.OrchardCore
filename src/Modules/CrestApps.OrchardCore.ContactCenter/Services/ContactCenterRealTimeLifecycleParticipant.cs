using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterRealTimeLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ContactCenterHubConnectionRegistry _connectionRegistry;
    private readonly TimeSpan _drainTimeout;

    public ContactCenterRealTimeLifecycleParticipant(
        IContactCenterFeatureWorkManager workManager,
        ContactCenterHubConnectionRegistry connectionRegistry,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        _workManager = workManager;
        _connectionRegistry = connectionRegistry;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string FeatureId => ContactCenterConstants.Feature.RealTime;

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(FeatureId);
        _connectionRegistry.Quiesce();

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(FeatureId, _drainTimeout, cancellationToken);
    }

    public Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        _connectionRegistry.Activate();
        _workManager.Activate(FeatureId);

        return Task.CompletedTask;
    }
}
