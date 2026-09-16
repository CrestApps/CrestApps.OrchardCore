using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// <para>
/// Each run is bounded by a wall-clock budget enforced both by a between-profile deadline check and a hard
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow run can never outlive its lock and let a
/// second node begin an overlapping pacing cycle. Profiles that do not fit in the budget are simply paced on the
/// following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Dialer Pacing",
    Schedule = "* * * * *",
    Description = "Reserves agents and places outbound calls for enabled dialer profiles.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class DialerPacingBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping pacing cycle on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.DialerPaced);

        if (workLease is null)
        {
            return;
        }

        var dialerManager = serviceProvider.GetRequiredService<IDialerProfileManager>();
        var dialerService = serviceProvider.GetRequiredService<IDialerService>();
        var queueItemStore = serviceProvider.GetRequiredService<IQueueItemStore>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<DialerPacingBackgroundTask>>();

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        var runDeadlineUtc = clock.UtcNow.AddMilliseconds(MaxRunDurationMilliseconds);

        // Pacing is work-driven: a dialer profile is now reusable settings chosen when inventory is loaded, and
        // each loaded activity carries its profile on the queue item. So instead of iterating profiles, find the
        // campaign queues that actually have waiting outbound inventory and pace each one with the profile the
        // work was loaded under.
        IReadOnlyCollection<string> waitingQueueIds;

        try
        {
            waitingQueueIds = await queueItemStore.GetWaitingQueueIdsAsync(runToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while listing queues; deferring to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        var campaignQueueIds = waitingQueueIds
            .Where(ContactCenterConstants.IsCampaignQueue)
            .ToArray();

        foreach (var queueId in campaignQueueIds)
        {
            if (clock.UtcNow >= runDeadlineUtc)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                // Resolve the profile the queue's waiting inventory was loaded under from its head item. A campaign
                // is normally dialed by one profile; when several profiles share a campaign queue, the head item's
                // profile governs this cycle's pacing.
                var headItem = await queueItemStore.FindNextWaitingAsync(queueId, runToken);

                if (headItem is null || string.IsNullOrEmpty(headItem.DialerProfileId))
                {
                    continue;
                }

                var profile = await dialerManager.FindByIdAsync(headItem.DialerProfileId, runToken);

                if (profile is null)
                {
                    continue;
                }

                await dialerService.RunCycleAsync(profile, queueId, runToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while pacing queue '{QueueId}'; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queueId);
                }

                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while pacing dialer queue '{QueueId}'.", queueId);
            }
        }
    }
}
