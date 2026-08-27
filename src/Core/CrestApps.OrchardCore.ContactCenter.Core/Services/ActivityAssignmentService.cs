using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityAssignmentService"/>. It pairs the
/// highest-priority waiting item with the agent who has been available the longest (round robin by idle time).
/// Assignment for a queue is serialized with a per-queue distributed lock so that two nodes, or the
/// reservation-expiry background task running alongside an inbound call, cannot double-assign the same
/// item or agent.
/// </summary>
public sealed class ActivityAssignmentService : IActivityAssignmentService
{
    private static readonly TimeSpan _assignmentLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _assignmentLockExpiration = TimeSpan.FromSeconds(30);

    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityRoutingService _routingService;
    private readonly IActivityReservationService _reservationService;
    private readonly IBusinessHoursService _businessHours;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityAssignmentService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="availabilityService">The canonical agent availability service.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="routingService">The routing service.</param>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="businessHours">The business-hours service used to pause assignment while the queue is closed.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="distributedLock">The distributed lock used to serialize assignment per queue.</param>
    /// <param name="session">The YesSql session used to persist each reservation before assigning more queue work.</param>
    /// <param name="clock">The clock used to evaluate SLA aging and business hours.</param>
    /// <param name="logger">The logger.</param>
    public ActivityAssignmentService(
        IQueueItemManager queueItemManager,
        IAgentAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IActivityRoutingService routingService,
        IActivityReservationService reservationService,
        IBusinessHoursService businessHours,
        IContactCenterEventPublisher publisher,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<ActivityAssignmentService> logger)
    {
        _queueItemManager = queueItemManager;
        _availabilityService = availabilityService;
        _queueManager = queueManager;
        _routingService = routingService;
        _reservationService = reservationService;
        _businessHours = businessHours;
        _publisher = publisher;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetQueueLockKey(queueId),
            _assignmentLockTimeout,
            _assignmentLockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Skipped assigning the next Contact Center item for queue '{QueueId}' because its assignment lock was not acquired.",
                    queueId.SanitizeLogValue());
            }

            return null;
        }

        await using var acquiredLock = locker;

        return await AssignNextCoreAsync(queueId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetQueueLockKey(queueId),
            _assignmentLockTimeout,
            _assignmentLockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Skipped assigning Contact Center queue '{QueueId}' because its assignment lock was not acquired.",
                    queueId.SanitizeLogValue());
            }

            return 0;
        }

        await using var acquiredLock = locker;

        var count = 0;

        while (await AssignNextCoreAsync(queueId, cancellationToken) is not null)
        {
            count++;
            await _session.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AssignSpecificAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(queueId);
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetQueueLockKey(queueId),
            _assignmentLockTimeout,
            _assignmentLockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Skipped a direct-to-agent assignment for queue '{QueueId}' because its assignment lock was not acquired.",
                    queueId.SanitizeLogValue());
            }

            return null;
        }

        await using var acquiredLock = locker;

        // A direct-to-agent (personal line) offer is carried under the synthetic direct-routing queue, which has
        // no persisted queue row: there is nothing to look up or enable, availability is evaluated directly
        // against the named agent (no queue entitlement or sign-in), and the reservation uses the direct-routing
        // timeout. A real-queue offer keeps the queue lookup and queue-scoped availability.
        var isDirect = ContactCenterConstants.IsDirectRoutingQueue(queueId);
        int timeout;

        if (isDirect)
        {
            // The direct offer rings the agent for the entry point's configured ring window (falling back to the
            // default when unspecified), after which the reservation expires and the caller is sent to voicemail.
            timeout = ringTimeoutSeconds is > 0
                ? ringTimeoutSeconds.Value
                : ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;
        }
        else
        {
            var queue = ContactCenterConstants.IsCampaignQueue(queueId)
                ? CampaignRoutingQueue.Create(queueId)
                : await _queueManager.FindByIdAsync(queueId, cancellationToken);

            if (queue is null || !queue.Enabled)
            {
                return null;
            }

            timeout = queue.ReservationTimeoutSeconds > 0
                ? queue.ReservationTimeoutSeconds
                : 30;
        }

        var queueItem = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        // Only a still-waiting item can be reserved. If the item was already reserved/assigned (for example a
        // concurrent queue sweep grabbed it) the direct offer yields to that outcome.
        if (queueItem is null || queueItem.Status != QueueItemStatus.Waiting)
        {
            return null;
        }

        // The availability service returns null when the named agent cannot take the call. For a direct offer
        // that means not present/Available, no live session, or at capacity; for a queue offer it additionally
        // means not entitled to or signed into the queue.
        var availability = isDirect
            ? await _availabilityService.GetForDirectAsync(agentId, cancellationToken)
            : await _availabilityService.GetAsync(agentId, queueId, cancellationToken);

        if (availability?.Agent is null)
        {
            return null;
        }

        var reservation = await _reservationService.ReserveAsync(queueItem, availability.Agent, timeout, cancellationToken);

        if (reservation is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Directly reserved Contact Center queue item '{QueueItemId}' as reservation '{ReservationId}' for agent '{AgentId}' in queue '{QueueId}'.",
                queueItem.ItemId.SanitizeLogValue(),
                reservation.ItemId.SanitizeLogValue(),
                agentId.SanitizeLogValue(),
                queueId.SanitizeLogValue());
        }

        return reservation;
    }

    private async Task<ActivityReservation> AssignNextCoreAsync(string queueId, CancellationToken cancellationToken)
    {
        var queue = ContactCenterConstants.IsCampaignQueue(queueId)
            ? CampaignRoutingQueue.Create(queueId)
            : await _queueManager.FindByIdAsync(queueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Skipped Contact Center assignment for queue '{QueueId}' because the queue is {QueueState}.",
                    queueId.SanitizeLogValue(),
                    queue is null ? "missing" : "disabled");
            }

            return null;
        }

        var now = _clock.UtcNow;

        if (!await _businessHours.IsOpenAsync(queue.BusinessHoursCalendarId, now, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Skipped Contact Center assignment for queue '{QueueId}' because its business hours are closed.",
                    queueId.SanitizeLogValue());
            }

            return null;
        }

        var topItem = await _queueItemManager.FindNextWaitingAsync(queue, now, cancellationToken);

        if (topItem is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "No waiting Contact Center item is available for queue '{QueueId}'.",
                    queueId.SanitizeLogValue());
            }

            return null;
        }

        var availability = await _availabilityService.GetForQueueAsync(queueId, cancellationToken);
        var agents = availability.Select(entry => entry.Agent).ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Evaluating Contact Center queue item '{QueueItemId}' for queue '{QueueId}' against {AvailableAgentCount} available agents.",
                topItem.ItemId.SanitizeLogValue(),
                queueId.SanitizeLogValue(),
                agents.Length);
        }

        var decision = await _routingService.SelectAgentAsync(queue, topItem, agents, cancellationToken);

        if (!decision.Succeeded || decision.Agent is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                var candidateSummary = string.Join(
                    "; ",
                    decision.Candidates.Select(candidate =>
                        $"{candidate.Agent.ItemId.SanitizeLogValue()}: eligible={candidate.IsEligible}, reasonCount={candidate.Reasons.Count}"));

                _logger.LogWarning(
                    "Contact Center routing did not assign queue item '{QueueItemId}' from queue '{QueueId}'. Reason: {Reason}. Candidates: {CandidateSummary}",
                    topItem.ItemId.SanitizeLogValue(),
                    queueId.SanitizeLogValue(),
                    decision.Reason.SanitizeLogValue(),
                    candidateSummary);
            }

            await PublishRoutingDecisionAsync(decision, cancellationToken);

            return null;
        }

        var timeout = queue.ReservationTimeoutSeconds > 0
            ? queue.ReservationTimeoutSeconds
            : 30;

        var reservation = await _reservationService.ReserveAsync(topItem, decision.Agent, timeout, cancellationToken);

        if (reservation is null)
        {
            decision.Succeeded = false;
            decision.Reason = "The selected agent or queue item was no longer available when the reservation was created.";

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Contact Center reservation creation lost a race for queue item '{QueueItemId}' and agent '{AgentId}' in queue '{QueueId}'.",
                    topItem.ItemId.SanitizeLogValue(),
                    decision.Agent.ItemId.SanitizeLogValue(),
                    queueId.SanitizeLogValue());
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Reserved Contact Center queue item '{QueueItemId}' as reservation '{ReservationId}' for agent '{AgentId}' in queue '{QueueId}'.",
                    topItem.ItemId.SanitizeLogValue(),
                    reservation.ItemId.SanitizeLogValue(),
                    decision.Agent.ItemId.SanitizeLogValue(),
                    queueId.SanitizeLogValue());
            }
        }

        await PublishRoutingDecisionAsync(decision, cancellationToken);

        return reservation;
    }

    private Task PublishRoutingDecisionAsync(ActivityRoutingDecision decision, CancellationToken cancellationToken)
    {
        var data = new ActivityRoutingDecisionEventData
        {
            QueueId = decision.Queue?.ItemId,
            QueueItemId = decision.QueueItem?.ItemId,
            ActivityItemId = decision.QueueItem?.ActivityItemId,
            SelectedAgentId = decision.Agent?.ItemId,
            Succeeded = decision.Succeeded,
            Reason = decision.Reason,
            Candidates = decision.Candidates
                .Select(candidate => new ActivityRoutingCandidateDecisionData
                {
                    AgentId = candidate.Agent.ItemId,
                    UserId = candidate.Agent.UserId,
                    IsEligible = candidate.IsEligible,
                    Score = candidate.Score,
                    Reasons = [.. candidate.Reasons],
                })
                .ToArray(),
        };

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            AggregateType = nameof(QueueItem),
            AggregateId = decision.QueueItem?.ItemId,
            ActorId = decision.Agent?.ItemId,
            SourceComponent = ContactCenterConstants.Components.Routing,
        };

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    private static string GetQueueLockKey(string queueId)
    {
        return $"ContactCenterQueueAssignment:{queueId}";
    }
}
