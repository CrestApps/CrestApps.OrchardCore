using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Support;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

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
public sealed class ReservationExpiryCycle : IReservationExpiryCycle
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

    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IActivityReservationService _reservationService;
    private readonly IDirectHoldTimeoutService _directHoldTimeoutService;
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IActivityQueueService _queueService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IQueueItemStore _queueItemStore;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly TimeProvider _timeProvider;
    private readonly IStoreCommitter _storeCommitter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReservationExpiryCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="directHoldTimeoutServices">
    /// The direct-to-agent hold timeout, which only the Voice feature registers. Taken as a sequence
    /// because expiring due reservations is still worth doing without it.
    /// </param>
    /// <param name="assignmentService">The assignment service.</param>
    /// <param name="queueService">The queue service.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="queueItemStore">The queue item store.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="inboundVoiceService">The inbound voice service.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="storeCommitter">The commit boundary.</param>
    /// <param name="logger">The logger.</param>
    public ReservationExpiryCycle(
        IContactCenterFeatureWorkManager workManager,
        IActivityReservationService reservationService,
        IEnumerable<IDirectHoldTimeoutService> directHoldTimeoutServices,
        IActivityAssignmentService assignmentService,
        IActivityQueueService queueService,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IQueueItemStore queueItemStore,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IEnumerable<IInboundVoiceService> inboundVoiceService,
        TimeProvider timeProvider,
        IStoreCommitter storeCommitter,
        ILogger<ReservationExpiryCycle> logger)
    {
        _workManager = workManager;
        _reservationService = reservationService;
        _directHoldTimeoutService = directHoldTimeoutServices.FirstOrDefault();
        _assignmentService = assignmentService;
        _queueService = queueService;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _queueItemStore = queueItemStore;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _inboundVoiceService = inboundVoiceService.FirstOrDefault();
        _timeProvider = timeProvider;
        _storeCommitter = storeCommitter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Queues);

        if (workLease is null)
        {
            return;
        }


        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        var runDeadlineUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(MaxRunDurationMilliseconds);

        IReadOnlyCollection<ActivityQueue> queues;

        try
        {
            await _reservationService.ExpireDueAsync(runToken);

            // Bound how long direct-to-agent (personal line) calls are held: send elapsed ring windows to
            // voicemail, and re-offer voicemail-disabled holds to their agent when available.
            if (_directHoldTimeoutService is not null)
            {
                await _directHoldTimeoutService.ProcessDueAsync(runToken);
            }

            queues = await _queueManager.GetEnabledAsync(runToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while expiring reservations; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        foreach (var queue in queues)
        {
            if (_timeProvider.GetUtcNow().UtcDateTime >= runDeadlineUtc)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                await _queueService.OverflowDueAsync(queue, runToken);

                var voiceWorkBlockedGenericAssignment = false;

                if (_inboundVoiceService is not null)
                {
                    for (var attempt = 0; attempt < MaxVoiceOffersPerQueue; attempt++)
                    {
                        if (_timeProvider.GetUtcNow().UtcDateTime >= runDeadlineUtc)
                        {
                            voiceWorkBlockedGenericAssignment = true;

                            break;
                        }

                        var nextItem = await _queueItemManager.FindNextWaitingAsync(queue, _timeProvider.GetUtcNow().UtcDateTime, runToken);

                        if (nextItem is null)
                        {
                            break;
                        }

                        var interaction = await _interactionManager.FindByActivityIdAsync(nextItem.ActivityItemId, runToken);

                        if (interaction?.Channel != InteractionChannel.Voice ||
                            interaction.Direction != InteractionDirection.Inbound ||
                            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
                        {
                            break;
                        }

                        voiceWorkBlockedGenericAssignment = true;

                        if (string.IsNullOrWhiteSpace(await _inboundVoiceService.OfferNextAsync(queue.ItemId, runToken)))
                        {
                            break;
                        }

                        await _storeCommitter.CommitAsync(runToken);
                        voiceWorkBlockedGenericAssignment = attempt == MaxVoiceOffersPerQueue - 1;
                    }
                }

                if (voiceWorkBlockedGenericAssignment)
                {
                    continue;
                }

                var nextGenericItem = await _queueItemManager.FindNextWaitingAsync(queue, _timeProvider.GetUtcNow().UtcDateTime, runToken);

                if (nextGenericItem is not null)
                {
                    var activity = await _activityManager.FindByIdAsync(nextGenericItem.ActivityItemId, runToken);

                    // AI-automatic work is owned by the omnichannel AI voice processor; never offer it to an agent.
                    if (activity?.InteractionType == ActivityInteractionType.Automated)
                    {
                        continue;
                    }

                    if (activity?.Source is ActivitySources.PowerDial or ActivitySources.ProgressiveDial)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug(
                                "Skipped generic assignment for automated dialer activity '{ActivityItemId}' in queue '{QueueId}'. The dialer pacing task owns {ActivitySource} work.",
                                activity.ItemId.SanitizeLogValue(),
                                queue.ItemId.SanitizeLogValue(),
                                activity.Source);
                        }

                        continue;
                    }
                }

                await _assignmentService.AssignQueueAsync(queue.ItemId, runToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while processing queue '{QueueId}'; deferring the remaining work to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queue.ItemId.SanitizeLogValue());
                }

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
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
            var waitingQueueIds = await _queueItemStore.GetWaitingQueueIdsAsync(runToken);

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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while listing campaign queues; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        foreach (var queueId in waitingCampaignQueueIds)
        {
            if (_timeProvider.GetUtcNow().UtcDateTime >= runDeadlineUtc)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget; deferring the remaining campaign queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                var headItem = await _queueItemStore.FindNextWaitingAsync(queueId, runToken);

                if (headItem is null)
                {
                    continue;
                }

                var activity = await _activityManager.FindByIdAsync(headItem.ActivityItemId, runToken);

                // AI-automatic work is owned end to end by the omnichannel AI voice processor and must never be
                // offered to a human agent. It carries no Contact Center interaction, so an agent offer can only be
                // rejected and re-swept in a loop; more importantly this is not agent-staffed work at all.
                if (activity?.InteractionType == ActivityInteractionType.Automated)
                {
                    continue;
                }

                // Automated dialer inventory is paced and offered by DialerPacingBackgroundTask; skip it here so
                // the pacer remains the sole owner of Power/Progressive (and the blocked Predictive) work.
                if (activity?.Source is ActivitySources.PowerDial or ActivitySources.ProgressiveDial or ActivitySources.PredictiveDial)
                {
                    continue;
                }

                await _assignmentService.AssignQueueAsync(queueId, runToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The reservation-and-assignment run reached its {BudgetMilliseconds} ms time budget while assigning campaign queue '{QueueId}'; deferring the remaining work to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queueId.SanitizeLogValue());
                }

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while assigning work for campaign queue '{QueueId}'.",
                    queueId.SanitizeLogValue());
            }
        }
    }
}
