using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Plays what waiting callers are due to hear, and hands on the callers whose overflow tier has come due.
/// </summary>
/// <remarks>
/// Both jobs are timing-sensitive in a way the once-a-minute sweep could not serve: a queue set to overflow after
/// twenty seconds actually overflowed somewhere between sixty and eighty, and an announcement every thirty
/// seconds arrived every sixty. Orchard schedules background tasks by cron, whose finest granularity is a minute,
/// so this task is scheduled every minute and sweeps repeatedly inside its own run — which gives ten-second
/// precision without a second scheduling mechanism. The run is bounded well inside the distributed lock's
/// expiration so a slow pass can never outlive its lock and let another node sweep the same queues.
/// <para>
/// Each sweep runs in its own shell scope, and that is load-bearing rather than tidiness. Sending a caller to
/// voicemail or overflowing them registers a provider command and schedules its dispatch for after the scope
/// commits, because the command row has to be committed before another scope can pick it up. With one scope
/// around the whole run, "after commit" meant "after the last sweep": a caller hit their maximum wait, the hold
/// music stopped at once, and the voicemail greeting arrived up to a run later. It measured twenty seconds of
/// dead air on one call and forty on another. A scope per sweep keeps the commit-ordering guarantee and brings
/// the delay back down to the sweep interval, which is the precision this task exists to provide.
/// </para>
/// </remarks>
[BackgroundTask(
    Title = "Contact Center Queue Treatment",
    Schedule = "* * * * *",
    Description = "Plays queue announcements and callback offers to waiting callers, overflows callers whose tier is due, and applies the maximum-wait action to callers who have waited past it.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class QueueTreatmentBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Twice the one-minute schedule, so the lock is not
    /// released while a run is still sweeping.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The wall-clock budget for one run. Kept below the lock expiration so a run always finishes before its lock
    /// can expire.
    /// </summary>
    private static readonly TimeSpan _runBudget = TimeSpan.FromSeconds(50);

    /// <summary>
    /// How long to wait between sweeps. This is the precision the acceptance criterion asks for: a caller
    /// overflows within ten seconds of their threshold rather than up to a minute after it.
    /// </summary>
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IQueueTreatmentCycle>().RunAsync(cancellationToken);
}
