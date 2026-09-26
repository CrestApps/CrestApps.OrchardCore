using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// The durable backstop for waiting callers: once a minute it plays what each queue's callers are due, re-arms each
/// queue's next treatment deadline, hands on callers whose overflow tier has come due, and applies the maximum-wait
/// action to callers who have waited past it.
/// </summary>
/// <remarks>
/// Treatment and wait deadlines are timed to the second by the in-process deadlines
/// <see cref="IContactCenterDeadlineScheduler"/> holds — a queue's next announcement, a caller's next overflow hop or
/// maximum wait — and arriving callers arm them as they arrive. This task used to provide that precision itself by
/// sweeping every ten seconds for fifty seconds of each minute, but Orchard runs a tenant's background tasks one after
/// another, so holding the loop that long delayed every other task behind it. It now makes one pass and returns the
/// loop; the pass catches what the deadlines missed (a restart, work armed on another node) and re-arms them.
/// <para>
/// The pass runs in its own shell scope, and that is load-bearing rather than tidiness. Sending a caller to voicemail
/// or overflowing them registers a provider command and schedules its dispatch for after the scope commits, because
/// the command row has to be committed before another scope can pick it up.
/// </para>
/// </remarks>
[BackgroundTask(
    Title = "Contact Center Queue Treatment",
    Schedule = "* * * * *",
    Description = "Plays queue announcements and callback offers to waiting callers, overflows callers whose tier is due, and applies the maximum-wait action to callers who have waited past it.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class QueueTreatmentBackgroundTask : IBackgroundTask
{
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

        try
        {
            await scopeExecutor.ExecuteAsync(scoped => SweepAsync(scoped, logger, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tenant is shutting down; the next scheduled run picks the sweep back up.
        }
    }

    /// <summary>
    /// Sweeps every queue once.
    /// </summary>
    private static async Task SweepAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken cancellationToken)
    {
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var treatmentEnforcer = serviceProvider.GetRequiredService<IQueueTreatmentDeadlineEnforcer>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var limitService = serviceProvider.GetRequiredService<IQueueLimitService>();

        var queues = await queueManager.GetAllAsync(cancellationToken);

        foreach (var queue in queues)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Plays what is due now and holds the queue's next deadline, so the next announcement is played on
                // time rather than on the next run.
                await treatmentEnforcer.RunAndArmAsync(queue.ItemId, cancellationToken);
                await queueService.OverflowDueAsync(queue, cancellationToken);

                // After the overflow tiers, so a caller whose hop and maximum wait fall due together is
                // handed on rather than sent to voicemail.
                await limitService.EnforceMaxWaitAsync(queue, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
