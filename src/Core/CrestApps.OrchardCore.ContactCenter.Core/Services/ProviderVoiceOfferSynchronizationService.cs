using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles routing state when provider truth reports that a queued, offered, or assigned call ended.
/// </summary>
public sealed class ProviderVoiceOfferSynchronizationService : IProviderVoiceOfferSynchronizationService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IServiceProvider _serviceProvider;
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
    /// <param name="serviceProvider">The service provider used to lazily resolve presence management without an event-publisher cycle.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ProviderVoiceOfferSynchronizationService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IQueueItemManager queueItemManager,
        IActivityReservationManager reservationManager,
        IAgentProfileManager agentManager,
        IOmnichannelActivityManager activityManager,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<ProviderVoiceOfferSynchronizationService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _queueItemManager = queueItemManager;
        _reservationManager = reservationManager;
        _agentManager = agentManager;
        _activityManager = activityManager;
        _serviceProvider = serviceProvider;
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
        var reservations = await _reservationManager.ListActiveByActivityAsync(interaction.ActivityItemId, cancellationToken);
        var providerReportedAnswered = interaction.AnsweredUtc.HasValue || session?.AnsweredUtc.HasValue == true;
        var wasAnsweredByAgent = providerReportedAnswered &&
            (queueItem?.Status == QueueItemStatus.Assigned ||
                reservations.Any(reservation => reservation.Status == ReservationStatus.Accepted));
        var canceledReservationIds = new HashSet<string>(StringComparer.Ordinal);
        string reservationAgentId = null;

        foreach (var reservation in reservations)
        {
            reservationAgentId ??= reservation.AgentId;
            reservation.Status = ReservationStatus.Canceled;
            await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);
            canceledReservationIds.Add(reservation.ItemId);
        }

        if (wasAnsweredByAgent)
        {
            if (queueItem?.Status == QueueItemStatus.Assigned)
            {
                queueItem.Status = QueueItemStatus.Completed;
                queueItem.DequeuedUtc = _clock.UtcNow;
                await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
            }

            var answeredAgentId = session?.AgentId ?? interaction.AgentId ?? reservationAgentId;

            if (!string.IsNullOrWhiteSpace(answeredAgentId))
            {
                var presenceManager = _serviceProvider.GetRequiredService<IAgentPresenceManager>();
                var agent = await _agentManager.FindByIdAsync(answeredAgentId, cancellationToken);
                var activityWasCompleted = activity?.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged;

                if (activityWasCompleted &&
                    agent is not null &&
                    string.IsNullOrWhiteSpace(agent.ActiveReservationId) &&
                    agent.PresenceStatus is AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp)
                {
                    await presenceManager.CompleteWorkAsync(answeredAgentId, cancellationToken);
                }
                else if (!activityWasCompleted)
                {
                    await presenceManager.StartWrapUpAsync(answeredAgentId, cancellationToken);
                }
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Provider truth ended pre-connect interaction '{InteractionId}'. Clearing stale queue and offer state for activity '{ActivityItemId}'.",
                OperationalLogRedactor.Pseudonymize(interaction.ItemId, OperationalLogIdentifierCategory.Interaction),
                OperationalLogRedactor.Pseudonymize(interaction.ActivityItemId, OperationalLogIdentifierCategory.Activity));
        }

        if (queueItem is not null &&
            queueItem.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            queueItem.Status = QueueItemStatus.Removed;
            queueItem.DequeuedUtc = _clock.UtcNow;
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
        }

        var agentId = reservationAgentId ?? session?.AgentId ?? interaction.AgentId;

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

            if (agent is not null)
            {
                if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId) &&
                    canceledReservationIds.Contains(agent.ActiveReservationId))
                {
                    agent.ActiveReservationId = null;
                }

                if (agent.PresenceStatus is AgentPresenceStatus.Reserved or
                    AgentPresenceStatus.Busy or
                    AgentPresenceStatus.WrapUp)
                {
                    agent.PresenceStatus = AgentPresenceUtilities.ResolveDefaultReadyState(agent);
                }

                agent.RequestedPresenceStatus = null;
                agent.PresenceChangedUtc = _clock.UtcNow;
                await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
            }
        }

        if (activity is null)
        {
            return;
        }

        activity.AssignmentStatus = ActivityAssignmentStatus.Released;
        activity.AssignedToId = null;
        activity.AssignedToUsername = null;
        activity.AssignedToUtc = null;
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
    }

    private static bool IsTerminalState(ContactCenterCallState? state)
    {
        return state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred;
    }
}
