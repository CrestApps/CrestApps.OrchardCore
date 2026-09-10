using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Queues);

        if (workLease is null)
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<QueueTreatmentBackgroundTask>>();
        var scopeExecutor = serviceProvider.GetRequiredService<IContactCenterScopeExecutor>();

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(_runBudget);
        var runToken = runCts.Token;

        try
        {
            while (!runToken.IsCancellationRequested)
            {
                await scopeExecutor.ExecuteAsync(scoped => SweepAsync(scoped, logger, runToken));

                await Task.Delay(_sweepInterval, runToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The run budget expired or the tenant is shutting down. Both end the run normally: the next
            // scheduled tick picks the sweep back up.
        }
    }

    /// <summary>
    /// Sweeps every queue once.
    /// </summary>
    private static async Task SweepAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken runToken)
    {
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var treatmentService = serviceProvider.GetRequiredService<IQueueTreatmentService>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var limitService = serviceProvider.GetRequiredService<IQueueLimitService>();

        var queues = await queueManager.GetAllAsync(runToken);

        foreach (var queue in queues)
        {
            if (runToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await treatmentService.RunDueAsync(queue, runToken);
                await queueService.OverflowDueAsync(queue, runToken);

                // After the overflow tiers, so a caller whose hop and maximum wait fall due together is
                // handed on rather than sent to voicemail.
                await limitService.EnforceMaxWaitAsync(queue, runToken);
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One misconfigured queue must not stop the sweep for every other queue on the tenant.
                logger.LogError(ex, "A queue-treatment pass failed for one queue; the remaining queues are still swept.");
            }
        }
    }
}
