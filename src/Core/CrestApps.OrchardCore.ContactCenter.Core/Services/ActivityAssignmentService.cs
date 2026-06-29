using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityAssignmentService"/>. It pairs the
/// highest-priority waiting item with the agent who has been available the longest (round robin by idle time).
/// </summary>
public sealed class ActivityAssignmentService : IActivityAssignmentService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityRoutingService _routingService;
    private readonly IActivityReservationService _reservationService;
    private readonly IContactCenterEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityAssignmentService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="routingService">The routing service.</param>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    public ActivityAssignmentService(
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        IActivityRoutingService routingService,
        IActivityReservationService reservationService,
        IContactCenterEventPublisher publisher)
    {
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _routingService = routingService;
        _reservationService = reservationService;
        _publisher = publisher;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var waiting = await _queueItemManager.ListWaitingAsync(queueId, cancellationToken);
        var topItem = waiting.FirstOrDefault();

        if (topItem is null)
        {
            return null;
        }

        var agents = await _agentManager.ListAvailableForQueueAsync(queueId, cancellationToken);
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            return null;
        }

        var decision = await _routingService.SelectAgentAsync(queue, topItem, agents, cancellationToken);
        await PublishRoutingDecisionAsync(decision, cancellationToken);

        if (!decision.Succeeded || decision.Agent is null)
        {
            return null;
        }

        var timeout = queue.ReservationTimeoutSeconds > 0
            ? queue.ReservationTimeoutSeconds
            : 30;

        return await _reservationService.ReserveAsync(topItem, decision.Agent, timeout, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var count = 0;

        while (await AssignNextAsync(queueId, cancellationToken) is not null)
        {
            count++;
        }

        return count;
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
}
