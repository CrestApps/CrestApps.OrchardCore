using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Primitives;
using OrchardCore.Environment.Cache;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Forwards Contact Center configuration changes to Orchard Core's signal, so an invalidation is
/// honoured across every process sharing the tenant's signal backplane.
/// </summary>
public sealed class SignalContactCenterConfigurationChangeNotifier : IContactCenterConfigurationChangeNotifier
{
    private readonly ISignal _signal;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalContactCenterConfigurationChangeNotifier"/> class.
    /// </summary>
    /// <param name="signal">The signal used to create and trip change tokens.</param>
    public SignalContactCenterConfigurationChangeNotifier(ISignal signal)
    {
        _signal = signal;
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string key)
        => _signal.GetToken(key);

    /// <inheritdoc/>
    public Task NotifyChangedAsync(string key)
        => _signal.SignalTokenAsync(key);
}
