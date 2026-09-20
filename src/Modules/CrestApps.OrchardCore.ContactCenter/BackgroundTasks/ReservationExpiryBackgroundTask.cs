using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Expires stale agent reservations and assigns waiting work to available agents across enabled queues, and
/// across the virtual campaign queues that carry agent-driven (Preview/Manual) outbound inventory — which the
/// enabled-queue sweep cannot see because campaign queues are never persisted.
/// It participates in the Routing feature's work-admission drain so it stops admitting work while that
/// feature is quiescing (and disposes its lease so a disable can drain), it honours the cancellation token
/// so it stops promptly on shutdown, and it bounds each run to a wall-clock budget (enforced both by an
/// between-operations deadline check and a hard <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/>
/// that cancels in-flight work) which is safely below the distributed-lock expiration so a slow run cannot
/// outlive its lock and overlap the next scheduled tick. Work that does not fit in the budget is simply
/// picked up on the following tick.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Reservation and Assignment",
    Schedule = "* * * * *",
    Description = "Expires stale reservations and assigns queued activities to available agents.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class ReservationExpiryBackgroundTask : IBackgroundTask
{
    private const int MaxVoiceOffersPerQueue = 100;

    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is
    /// not released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping run.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IReservationExpiryCycle>().RunAsync(cancellationToken);
}
