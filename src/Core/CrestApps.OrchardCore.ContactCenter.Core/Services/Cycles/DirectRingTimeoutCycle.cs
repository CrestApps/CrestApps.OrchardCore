using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Enforces agent ring windows at a fine granularity. Reservation expiry and the direct-to-agent hold timeout
/// (which sends an unanswered caller to the agent's voicemail once the configured ring window elapses) are also
/// swept by <see cref="ReservationExpiryBackgroundTask"/>, but that task runs on the one-minute background-task
/// schedule, which cannot honour a sub-minute ring window: a 30-second window could otherwise wait up to a full
/// minute for the next sweep, and the caller frequently hangs up first, so the call never reaches voicemail.
/// <para>
/// The background-task schedule is minute-granular, so this task instead ticks internally: on each one-minute
/// invocation it runs the expiry and hold-timeout sweep every few seconds for just under a minute, giving the
/// ring window a few seconds of enforcement latency instead of up to a minute. Each tick runs in its own
/// committed shell scope so the provider commands the sweep schedules (the actual "send to voicemail" action)
/// dispatch after commit exactly as they do on the one-minute sweep. Every sweep is bounded and idempotent:
/// expiry takes a per-reservation lock and skips anything another sweep is already transitioning, so overlapping
/// with the one-minute task is safe.
/// </para>
/// </summary>
public sealed class DirectRingTimeoutCycle : IDirectRingTimeoutCycle
{
    /// <summary>
    /// How often the expiry and hold-timeout sweep runs within a single invocation. This bounds how long past the
    /// configured ring window a caller can wait before voicemail. It is a balance: too frequent and it adds write
    /// pressure the database (SQLite in particular) contends over; ~15 seconds still enforces a 30-second window
    /// closely while keeping the sweep light.
    /// </summary>
    private const int TickIntervalMilliseconds = 15_000;

    /// <summary>
    /// How long a single invocation keeps ticking. Kept just under the one-minute schedule so an invocation
    /// always finishes before the next one is scheduled and the two never overlap.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 55_000;

    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IDirectHoldTimeoutService _directHoldTimeoutService;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRingTimeoutCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="directHoldTimeoutServices">
    /// The direct-to-agent hold timeout, which only the Voice feature registers. Taken as a sequence
    /// because there is still value in expiring due reservations promptly without it.
    /// </param>
    /// <param name="scopeExecutor">The scope executor.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public DirectRingTimeoutCycle(
        IContactCenterFeatureWorkManager workManager,
        IEnumerable<IDirectHoldTimeoutService> directHoldTimeoutServices,
        IContactCenterScopeExecutor scopeExecutor,
        TimeProvider timeProvider,
        ILogger<DirectRingTimeoutCycle> logger)
    {
        _workManager = workManager;
        _directHoldTimeoutService = directHoldTimeoutServices.FirstOrDefault();
        _scopeExecutor = scopeExecutor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // The direct-to-agent hold timeout is only registered when the Voice feature is enabled; without it there
        // is still value in expiring due reservations promptly, so the sweep runs either way.
        var hasDirectHoldTimeout = _directHoldTimeoutService is not null;

        var deadlineUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(MaxRunDurationMilliseconds);

        while (!cancellationToken.IsCancellationRequested && _timeProvider.GetUtcNow().UtcDateTime < deadlineUtc)
        {
            // Acquire the drain lease per tick (and release it before the delay) so a feature disable can still
            // drain without waiting out the whole invocation.
            using (var workLease = _workManager.TryEnter(ContactCenterCapabilities.Queues))
            {
                if (workLease is null)
                {
                    return;
                }

                try
                {
                    await _scopeExecutor.ExecuteAsync<IActivityReservationService>(
                        reservationService => reservationService.ExpireDueAsync(cancellationToken));

                    if (hasDirectHoldTimeout)
                    {
                        await _scopeExecutor.ExecuteAsync<IDirectHoldTimeoutService>(
                            directHoldTimeoutService => directHoldTimeoutService.ProcessDueAsync(cancellationToken));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while enforcing the direct-to-agent ring timeout.");
                }
            }

            try
            {
                await Task.Delay(TickIntervalMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
