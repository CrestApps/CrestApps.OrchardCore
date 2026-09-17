using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureWorkLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;

    public ContactCenterFeatureWorkLifecycleParticipant(
        string capability,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options)
    {
        ArgumentException.ThrowIfNullOrEmpty(capability);
        ArgumentNullException.ThrowIfNull(workManager);
        ArgumentNullException.ThrowIfNull(options);

        Capability = capability;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
    }

    public string Capability { get; }

    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(Capability);

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(Capability, _drainTimeout, cancellationToken);
    }
}
