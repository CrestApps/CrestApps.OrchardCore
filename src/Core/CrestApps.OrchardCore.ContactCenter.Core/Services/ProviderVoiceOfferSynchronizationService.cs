using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles routing state when provider truth reports that a queued, offered, or assigned call ended.
/// </summary>
public sealed partial class ProviderVoiceOfferSynchronizationService : IProviderVoiceOfferSynchronizationService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<IContactCenterAuditRecorder> _auditRecorder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceOfferSynchronizationService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="agentManager">The agent manager.</param>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="serviceProvider">The service provider used to lazily resolve presence management and the agent state
    /// transition point without an event-publisher cycle.</param>
    /// <param name="auditRecorder">The recorder that writes a withdrawn offer, a call leaving its queue and an abandon to
    /// the audit log. Lazy because an event handler depends on this service and the recorder depends on the publisher
    /// that dispatches to every handler.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ProviderVoiceOfferSynchronizationService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IQueueItemManager queueItemManager,
        IActivityReservationManager reservationManager,
        IAgentProfileManager agentManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IServiceProvider serviceProvider,
        Lazy<IContactCenterAuditRecorder> auditRecorder,
        IClock clock,
        ILogger<ProviderVoiceOfferSynchronizationService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _queueItemManager = queueItemManager;
        _reservationManager = reservationManager;
        _agentManager = agentManager;
        _activityManager = activityManager;
        _workStateService = workStateService;
        _serviceProvider = serviceProvider;
        _auditRecorder = auditRecorder;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ReconcileEndedOfferAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || string.IsNullOrWhiteSpace(interaction.ActivityItemId))
        {
            return;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (interaction.Status is not InteractionStatus.Ended and not InteractionStatus.Failed &&
            !IsTerminalState(session?.State))
        {
            return;
        }

        var queueItem = await _queueItemManager.FindByActivityIdAsync(interaction.ActivityItemId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(interaction.ActivityItemId, cancellationToken);

        // Cancel every lingering reservation bound to this activity. Reject/re-offer cycles can accumulate
        // multiple accepted reservations for the same activity, and leaving them behind keeps an agent's
        // ActiveReservationId pointing at dead work, which blocks all future offers.
        var reservations = await _reservationManager.GetActiveByActivityAsync(interaction.ActivityItemId, cancellationToken);
        var providerReportedAnswered = interaction.AnsweredUtc.HasValue || session?.AnsweredUtc.HasValue == true;

        // A caller the platform answered itself (to play the queue) reports answered too, so an answer alone does not
        // say an agent took the call. Any of these does: routing still holds the assignment, the accepted offer is
        // still open, the call's own ending already started the agent's wrap-up, or the topology has an agent leg
        // that was joined to the caller. The last two matter once the queue item and reservation have settled, which
        // otherwise read as a pre-connect offer and moved an agent in wrap-up straight back to Available.
        var wasAnsweredByAgent = providerReportedAnswered &&
            (queueItem?.Status == QueueItemStatus.Assigned ||
                reservations.Any(reservation => reservation.Status == ReservationStatus.Accepted) ||
                interaction.WrapUpStartedUtc.HasValue ||
                HadJoinedAgentLeg(session));
        var canceledReservationIds = new HashSet<string>(StringComparer.Ordinal);
        string reservationAgentId = null;

        // What this does to the agent is the call ending's consequence, so it takes the end's instant: the provider's
        // time for it, which can run ahead of this clock. Dated by this clock instead, the agent would be on record
        // as released before the call they were released from had ended.
        var endedUtc = interaction.EndedUtc ?? session?.EndedUtc;

        foreach (var reservation in reservations)
        {
            reservationAgentId ??= reservation.AgentId;
            var wasRinging = reservation.Status == ReservationStatus.Pending;
            reservation.TransitionTo(ReservationStatus.Canceled);

            // This is the age settled reservations are purged by.
            reservation.ModifiedUtc = _clock.UtcNow;

            await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);
            canceledReservationIds.Add(reservation.ItemId);

            if (wasRinging)
            {
                await RecordOfferWithdrawnAsync(reservation, interaction, cancellationToken);
            }
        }

        if (wasAnsweredByAgent)
        {
            if (queueItem?.Status == QueueItemStatus.Assigned)
            {
                queueItem.TransitionTo(QueueItemStatus.Completed);
                queueItem.DequeuedUtc = _clock.UtcNow;
                await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
            }

            var answeredAgentId = session?.AgentId ?? interaction.AgentId ?? reservationAgentId;

            if (!string.IsNullOrWhiteSpace(answeredAgentId))
            {
                var presenceManager = _serviceProvider.GetRequiredService<IAgentPresenceManager>();
                var agent = await _agentManager.FindByIdAsync(answeredAgentId, cancellationToken);
                var activityWasCompleted = activity?.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged;

                // Wrap-up (after-call work) is only for ACD-routed queue and campaign work. A direct-to-agent
                // (personal line) call has nothing to disposition, so the agent returns straight to a ready state
                // instead of being parked in wrap-up -- the same distinction the live provider-event path draws.
                var startsWrapUp = !activityWasCompleted &&
                    ContactCenterConstants.QueueStartsAfterCallWork(interaction.QueueId);

                if (!startsWrapUp &&
                    agent is not null &&
                    string.IsNullOrWhiteSpace(agent.ActiveReservationId) &&
                    agent.PresenceStatus is AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp)
                {
                    await presenceManager.CompleteWorkAsync(answeredAgentId, new AgentStateChangeContext { InteractionId = interaction.ItemId, ChangedUtc = endedUtc }, cancellationToken);
                }
                else if (startsWrapUp)
                {
                    await presenceManager.StartWrapUpAsync(answeredAgentId, new AgentStateChangeContext { InteractionId = interaction.ItemId, ChangedUtc = endedUtc }, cancellationToken);
                }
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Provider truth ended pre-connect interaction '{InteractionId}'. Clearing stale queue and offer state for activity '{ActivityItemId}'.",
                interaction.ItemId.SanitizeLogValue(),
                interaction.ActivityItemId.SanitizeLogValue());
        }

        if (queueItem is not null &&
            queueItem.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            var wasWaiting = queueItem.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved;
            queueItem.TransitionTo(QueueItemStatus.Removed);
            queueItem.DequeuedUtc = _clock.UtcNow;
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

            await RecordLeftQueueAsync(queueItem, interaction, session, wasWaiting, cancellationToken);
        }

        var agentId = reservationAgentId ?? session?.AgentId ?? interaction.AgentId;

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

            if (agent is not null)
            {
                var releasedReservationId = !string.IsNullOrWhiteSpace(agent.ActiveReservationId) &&
                    canceledReservationIds.Contains(agent.ActiveReservationId)
                    ? agent.ActiveReservationId
                    : null;

                if (releasedReservationId is not null)
                {
                    agent.ActiveReservationId = null;
                }

                var targetStatus = agent.PresenceStatus is AgentPresenceStatus.Reserved or
                    AgentPresenceStatus.Busy or
                    AgentPresenceStatus.WrapUp
                    ? AgentPresenceUtilities.ResolveDefaultReadyState(agent)
                    : agent.PresenceStatus;

                agent.RequestedPresenceStatus = null;

                // The provider says the call is over while routing still has the agent on it; the platform puts
                // the agent right, and the audit records that it did rather than letting the state change silently.
                // Resolved here rather than injected: the transition point records through the event publisher,
                // whose handlers are what construct this service.
                var stateTransitions = _serviceProvider.GetRequiredService<IAgentStateTransitionService>();

                await stateTransitions.TransitionAsync(agent, targetStatus, new AgentStateChangeContext
                {
                    Source = AgentStateChangeSources.Reconciled,
                    InteractionId = interaction.ItemId,
                    ReservationId = releasedReservationId,
                    ChangedUtc = endedUtc,
                }, cancellationToken);

                await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
            }
        }

        if (activity is null)
        {
            return;
        }

        await _workStateService.MutateAsync(activity.ItemId, workState =>
        {
            workState.TransitionTo(ActivityAssignmentStatus.Released);
            workState.AssignedToId = null;
            workState.AssignedToUsername = null;
            workState.AssignedToUtc = null;
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;
        }, cancellationToken);
    }

    private static bool HadJoinedAgentLeg(CallSession session)
        => session?.Legs.Any(leg => leg is not null && leg.Role == CallPartyRole.Agent && leg.AnsweredUtc.HasValue) == true;

    private static bool IsTerminalState(VoiceCallState? state)
    {
        return state is VoiceCallState.Ended or
            VoiceCallState.Failed or
            VoiceCallState.NoAnswer or
            VoiceCallState.Rejected or
            VoiceCallState.Canceled or
            VoiceCallState.Transferred;
    }
}
