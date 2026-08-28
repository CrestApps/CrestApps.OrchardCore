using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Queues);

        if (workLease is null)
        {
            return;
        }

        var reservationService = serviceProvider.GetRequiredService<IActivityReservationService>();
        var directHoldTimeoutService = serviceProvider.GetService<IDirectHoldTimeoutService>();
        var assignmentService = serviceProvider.GetRequiredService<IActivityAssignmentService>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var queueItemManager = serviceProvider.GetRequiredService<IQueueItemManager>();
        var queueItemStore = serviceProvider.GetRequiredService<IQueueItemStore>();
        var interactionManager = serviceProvider.GetRequiredService<IInteractionManager>();
        var activityManager = serviceProvider.GetRequiredService<IOmnichannelActivityManager>();
        var inboundVoiceService = serviceProvider.GetServices<IInboundVoiceService>().FirstOrDefault();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpiryBackgroundTask>>();

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        var runDeadlineUtc = clock.UtcNow.AddMilliseconds(MaxRunDurationMilliseconds);

        IReadOnlyCollection<ActivityQueue> queues;

        try
        {
            await reservationService.ExpireDueAsync(runToken);

            // Bound how long direct-to-agent (personal line) calls are held: send elapsed ring windows to
            // voicemail, and re-offer voicemail-disabled holds to their agent when available.
            if (directHoldTimeoutService is not null)
            {
                await directHoldTimeoutService.ProcessDueAsync(runToken);
            }

            queues = await queueManager.GetEnabledAsync(runToken);
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
                    "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while expiring reservations; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        foreach (var queue in queues)
        {
            if (clock.UtcNow >= runDeadlineUtc)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                await queueService.OverflowDueAsync(queue, runToken);

                var voiceWorkBlockedGenericAssignment = false;

                if (inboundVoiceService is not null)
                {
                    for (var attempt = 0; attempt < MaxVoiceOffersPerQueue; attempt++)
                    {
                        if (clock.UtcNow >= runDeadlineUtc)
                        {
                            voiceWorkBlockedGenericAssignment = true;

                            break;
                        }

                        var nextItem = await queueItemManager.FindNextWaitingAsync(queue, clock.UtcNow, runToken);

                        if (nextItem is null)
                        {
                            break;
                        }

                        var interaction = await interactionManager.FindByActivityIdAsync(nextItem.ActivityItemId, runToken);

                        if (interaction?.Channel != InteractionChannel.Voice ||
                            interaction.Direction != InteractionDirection.Inbound ||
                            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
                        {
                            break;
                        }

                        voiceWorkBlockedGenericAssignment = true;

                        if (string.IsNullOrWhiteSpace(await inboundVoiceService.OfferNextAsync(queue.ItemId, runToken)))
                        {
                            break;
                        }

                        await session.SaveChangesAsync(runToken);
                        voiceWorkBlockedGenericAssignment = attempt == MaxVoiceOffersPerQueue - 1;
                    }
                }

                if (voiceWorkBlockedGenericAssignment)
                {
                    continue;
                }

                var nextGenericItem = await queueItemManager.FindNextWaitingAsync(queue, clock.UtcNow, runToken);

                if (nextGenericItem is not null)
                {
                    var activity = await activityManager.FindByIdAsync(nextGenericItem.ActivityItemId, runToken);

                    if (activity?.Source is ActivitySources.PowerDial or ActivitySources.ProgressiveDial)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug(
                                "Skipped generic assignment for automated dialer activity '{ActivityItemId}' in queue '{QueueId}'. The dialer pacing task owns {ActivitySource} work.",
                                activity.ItemId.SanitizeLogValue(),
                                queue.ItemId.SanitizeLogValue(),
                                activity.Source);
                        }

                        continue;
                    }
                }

                await assignmentService.AssignQueueAsync(queue.ItemId, runToken);
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
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while processing queue '{QueueId}'; deferring the remaining work to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queue.ItemId.SanitizeLogValue());
                }

                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "An error occurred while assigning work for queue '{QueueId}'.",
                    queue.ItemId.SanitizeLogValue());
            }
        }

        // Campaign queues are virtual: outbound routing synthesizes them on demand and never persists them, so
        // the enabled-queue sweep above never sees them. Agent-driven outbound inventory (Preview and Manual)
        // still needs to be offered to available agents, and nothing else does it: the dialer pacing task owns
        // only the automated Power/Progressive modes and no-ops for the rest. Enumerate the campaign queues that
        // currently hold waiting inventory and run the same assignment for the agent-driven ones here, leaving
        // paced inventory to DialerPacingBackgroundTask so the two tasks never both drive one campaign queue.
        IReadOnlyCollection<string> waitingCampaignQueueIds;

        try
        {
            var waitingQueueIds = await queueItemStore.GetWaitingQueueIdsAsync(runToken);

            waitingCampaignQueueIds = waitingQueueIds
                .Where(ContactCenterConstants.IsCampaignQueue)
                .ToArray();
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
                    "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while listing campaign queues; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        foreach (var queueId in waitingCampaignQueueIds)
        {
            if (clock.UtcNow >= runDeadlineUtc)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget; deferring the remaining campaign queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                var headItem = await queueItemStore.FindNextWaitingAsync(queueId, runToken);

                if (headItem is null)
                {
                    continue;
                }

                var activity = await activityManager.FindByIdAsync(headItem.ActivityItemId, runToken);

                // Automated dialer inventory is paced and offered by DialerPacingBackgroundTask; skip it here so
                // the pacer remains the sole owner of Power/Progressive (and the blocked Predictive) work.
                if (activity?.Source is ActivitySources.PowerDial or ActivitySources.ProgressiveDial or ActivitySources.PredictiveDial)
                {
                    continue;
                }

                await assignmentService.AssignQueueAsync(queueId, runToken);
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
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while assigning campaign queue '{QueueId}'; deferring the remaining work to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queueId.SanitizeLogValue());
                }

                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "An error occurred while assigning work for campaign queue '{QueueId}'.",
                    queueId.SanitizeLogValue());
            }
        }
    }
}
