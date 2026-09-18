using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

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
[BackgroundTask(
    Title = "Contact Center Ring Timeout Enforcement",
    Schedule = "* * * * *",
    Description = "Sends unanswered agent offers to voicemail at the configured ring window instead of on the one-minute sweep.",
    LockTimeout = 1_000,
    LockExpiration = 60_000)]
public sealed class DirectRingTimeoutBackgroundTask : IBackgroundTask
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

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IDirectRingTimeoutCycle>().RunAsync(cancellationToken);
}
