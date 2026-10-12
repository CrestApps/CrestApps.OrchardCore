using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Stops this node taking on new routing work the moment the host begins to shut down, and gives the routing work
/// already under way a short while to finish.
/// </summary>
/// <remarks>
/// A node that is shutting down can still offer a waiting caller to an agent, assign queued work or place an
/// outbound call in its last seconds, and then die before the work it started is seen through: an agent rung for a
/// caller nobody connects, a customer who answers a dial that no node is following. Waiting callers are not at risk
/// from this node stopping, because the carrier holds the call and the queue is durable; what is at risk is work
/// started here and not finished here. So routing stops being admitted at once, and the queue it leaves is picked up
/// by the other nodes, or by this one when it is back, through the same sweeps that recover any restart.
/// <para>
/// Only the routing features are closed. Provider webhooks, provider commands and the agents' real-time connection
/// keep working until the process goes, so a caller who hangs up is still recorded and a connect an agent has just
/// accepted is still bridged.
/// </para>
/// <para>
/// A tenant singleton, so the registration on the host's stopping token is dropped with the shell when the tenant is
/// reloaded, rather than left behind to quiesce a container that no longer exists.
/// </para>
/// </remarks>
internal sealed class ContactCenterShutdownDrain : ModularTenantEvents, IDisposable
{
    /// <summary>
    /// The features whose new work is refused once the host is stopping: queue routing and assignment, the outbound
    /// dialer and its callbacks, and the paced dialer.
    /// </summary>
    internal static readonly string[] RoutingFeatures =
    [
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.Dialer,
        ContactCenterConstants.Feature.DialerPaced,
    ];

    private readonly Lock _gate = new();
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;
    private readonly ILogger _logger;

    private CancellationTokenRegistration _registration;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterShutdownDrain"/> class.
    /// </summary>
    /// <param name="lifetime">The host lifetime whose stopping signal starts the drain.</param>
    /// <param name="workManager">The tenant's work-admission gate.</param>
    /// <param name="options">The drain timeouts.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterShutdownDrain(
        IHostApplicationLifetime lifetime,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options,
        ILogger<ContactCenterShutdownDrain> logger)
    {
        _lifetime = lifetime;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.ShutdownDrainTimeoutSeconds);
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task ActivatedAsync()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return Task.CompletedTask;
            }

            _started = true;

            // A host already stopping runs the callback here and now, which is what it should do.
            _registration = _lifetime.ApplicationStopping.Register(static state => ((ContactCenterShutdownDrain)state).Drain(), this);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the routing features to new work and waits, briefly, for the work already admitted.
    /// </summary>
    /// <remarks>
    /// Blocking is deliberate: the host runs its stopping callbacks before it stops the background services and the
    /// server, so returning at once would let them cancel the very work this is waiting for. The wait is bounded and
    /// returns immediately when nothing is running, which is the usual case.
    /// </remarks>
    internal void Drain()
    {
        foreach (var featureId in RoutingFeatures)
        {
            _workManager.Quiesce(featureId);
        }

        _logger.LogInformation("The host is shutting down; this node has stopped offering, assigning and dialing Contact Center work. Waiting work stays queued for the next node to pick up.");

        if (_drainTimeout <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            // The drain tasks complete from the work's own threads, never from this one, so blocking cannot deadlock.
            Task.WhenAll(RoutingFeatures.Select(featureId => _workManager.DrainAsync(featureId, _drainTimeout)))
                .GetAwaiter()
                .GetResult();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Contact Center routing work was still running {Seconds} seconds into shutdown; the recovery sweeps will settle whatever it leaves unfinished.",
                _drainTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Shutdown must go on whatever happens here.
            _logger.LogWarning(ex, "Could not wait for Contact Center routing work to finish during shutdown.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _registration.Dispose();
    }
}
