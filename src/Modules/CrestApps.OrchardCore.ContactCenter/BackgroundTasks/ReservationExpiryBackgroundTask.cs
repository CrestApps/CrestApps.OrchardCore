using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Expires stale agent reservations and assigns waiting work to available agents across enabled queues.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Reservation and Assignment",
    Schedule = "* * * * *",
    Description = "Expires stale reservations and assigns queued activities to available agents.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class ReservationExpiryBackgroundTask : IBackgroundTask
{
    private const int MaxVoiceOffersPerQueue = 100;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reservationService = serviceProvider.GetRequiredService<IActivityReservationService>();
        var assignmentService = serviceProvider.GetRequiredService<IActivityAssignmentService>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var queueItemManager = serviceProvider.GetRequiredService<IQueueItemManager>();
        var interactionManager = serviceProvider.GetRequiredService<IInteractionManager>();
        var activityManager = serviceProvider.GetRequiredService<IOmnichannelActivityManager>();
        var inboundVoiceService = serviceProvider.GetServices<IInboundVoiceService>().FirstOrDefault();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpiryBackgroundTask>>();

        await reservationService.ExpireDueAsync(cancellationToken);

        var queues = await queueManager.ListEnabledAsync(cancellationToken);

        foreach (var queue in queues)
        {
            try
            {
                await queueService.OverflowDueAsync(queue, cancellationToken);

                var voiceWorkBlockedGenericAssignment = false;

                if (inboundVoiceService is not null)
                {
                    for (var attempt = 0; attempt < MaxVoiceOffersPerQueue; attempt++)
                    {
                        var waitingItems = await queueItemManager.ListWaitingAsync(queue.ItemId, cancellationToken);
                        var nextItem = QueueItemPrioritizer.SelectNext(waitingItems, queue, clock.UtcNow);

                        if (nextItem is null)
                        {
                            break;
                        }

                        var interaction = await interactionManager.FindByActivityIdAsync(nextItem.ActivityItemId, cancellationToken);

                        if (interaction?.Channel != InteractionChannel.Voice ||
                            interaction.Direction != InteractionDirection.Inbound ||
                            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
                        {
                            break;
                        }

                        voiceWorkBlockedGenericAssignment = true;

                        if (string.IsNullOrWhiteSpace(await inboundVoiceService.OfferNextAsync(queue.ItemId, cancellationToken)))
                        {
                            break;
                        }

                        await session.SaveChangesAsync(cancellationToken);
                        voiceWorkBlockedGenericAssignment = attempt == MaxVoiceOffersPerQueue - 1;
                    }
                }

                if (voiceWorkBlockedGenericAssignment)
                {
                    continue;
                }

                var genericWaitingItems = await queueItemManager.ListWaitingAsync(queue.ItemId, cancellationToken);
                var nextGenericItem = QueueItemPrioritizer.SelectNext(genericWaitingItems, queue, clock.UtcNow);

                if (nextGenericItem is not null)
                {
                    var activity = await activityManager.FindByIdAsync(nextGenericItem.ActivityItemId, cancellationToken);

                    if (activity?.Source is ActivitySources.PowerDial or ActivitySources.ProgressiveDial)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug(
                                "Skipped generic assignment for automated dialer activity '{ActivityItemId}' in queue '{QueueId}'. The dialer pacing task owns {ActivitySource} work.",
                                OperationalLogRedactor.Pseudonymize(activity.ItemId, OperationalLogIdentifierCategory.Activity),
                                OperationalLogRedactor.Pseudonymize(queue.ItemId, OperationalLogIdentifierCategory.Queue),
                                activity.Source);
                        }

                        continue;
                    }
                }

                await assignmentService.AssignQueueAsync(queue.ItemId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    OperationalLogRedactor.RedactException(ex),
                    "An error occurred while assigning work for queue '{QueueId}'.",
                    OperationalLogRedactor.Pseudonymize(queue.ItemId, OperationalLogIdentifierCategory.Queue));
            }
        }
    }
}
