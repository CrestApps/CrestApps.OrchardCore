using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Recovers agent capacity when after-call work is orphaned or exceeds the configured deadline, and when an agent is
/// left Busy after the call they accepted is over.
/// </summary>
public sealed class AgentAvailabilityRecoveryService : IAgentAvailabilityRecoveryService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IInteractionEventStore _eventStore;
    private readonly AgentAvailabilityOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityRecoveryService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="reservationManagers">The reservation manager, registered only with queues and campaigns.</param>
    /// <param name="eventStore">The event log the agents' state changes are read from.</param>
    /// <param name="options">The availability policy.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentAvailabilityRecoveryService(
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IAgentPresenceManager presenceManager,
        IEnumerable<IActivityReservationManager> reservationManagers,
        IInteractionEventStore eventStore,
        IOptions<AgentAvailabilityOptions> options,
        IClock clock,
        ILogger<AgentAvailabilityRecoveryService> logger)
    {
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _presenceManager = presenceManager;
        _reservationManager = reservationManagers?.FirstOrDefault();
        _eventStore = eventStore;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
        => await RecoverWrapUpAsync(cancellationToken) +
            await RecoverOrphanedBusyAsync(cancellationToken) +
            await RecoverStrandedReservedAsync(cancellationToken);

    // Reserved says an offer is ringing for the agent, and the offer settling is what moves them on. An offer settled
    // without releasing them -- a power-dial attempt suppressed while a leftover accepted reservation was on file did
    // exactly that -- leaves an agent nobody routes to again: routing wants Available, the phone still reads
    // Available, and only the agent changing their own state used to clear it. Once no offer is left ringing for
    // them, nothing is live, and the grace period has passed, they are returned to the state they were ready in.
    private async Task<int> RecoverStrandedReservedAsync(CancellationToken cancellationToken)
    {
        if (_reservationManager is null)
        {
            return 0;
        }

        var agents = await _agentManager.GetByPresenceAsync(AgentPresenceStatus.Reserved, cancellationToken) ?? [];
        var now = _clock.UtcNow;
        var recovered = 0;

        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((agent.PresenceChangedUtc ?? DateTime.MinValue) + _options.OrphanedBusyGracePeriod > now)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(agent.ActiveReservationId))
            {
                var reservation = await _reservationManager.FindByIdAsync(agent.ActiveReservationId, cancellationToken);

                if (reservation is not null &&
                    (reservation.Status is ReservationStatus.Pending or ReservationStatus.Accepted ||
                        (reservation.ModifiedUtc ?? reservation.CreatedUtc) + _options.OrphanedBusyGracePeriod > now))
                {
                    continue;
                }
            }

            if (await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken) is not null ||
                await _interactionManager.CountActiveByAgentAsync(agent.ItemId, cancellationToken) > 0)
            {
                continue;
            }

            var settledReservationId = agent.ActiveReservationId;
            var target = agent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(agent);

            try
            {
                if (!string.IsNullOrEmpty(settledReservationId))
                {
                    // The presence manager applies a state at once only to an agent with no reservation on file.
                    agent.ActiveReservationId = null;
                    await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
                }

                await _presenceManager.SetPresenceAsync(agent.UserId, target, reason: null, new AgentStateChangeContext
                {
                    Source = AgentStateChangeSources.Reconciled,
                    ReservationId = settledReservationId,
                }, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipped reserved-state recovery for contended Contact Center agent '{AgentId}'.",
                    agent.ItemId.SanitizeLogValue());

                continue;
            }

            _logger.LogWarning(
                "Returned Contact Center agent '{AgentId}' to {State}: they were still Reserved with no offer ringing for them (settled reservation '{ReservationId}').",
                agent.ItemId.SanitizeLogValue(),
                target,
                settledReservationId.SanitizeLogValue());

            recovered++;
        }

        return recovered;
    }

    // An agent is Busy from the moment they accept an offer, answer a colleague's consult or take a call over, until the
    // call's end releases them. When that release is missed -- live, a callback whose agent leg failed left the agent
    // Busy with nothing on the line, and a supervisor's Set Available waited for work that would never end -- the agent
    // is returned to work once the call that made them Busy is over and the grace period has passed.
    //
    // What made them Busy is read from their own state history rather than from their accepted reservations: accepted
    // reservations are never closed, so they pile up across the day (one of them an offer whose call was never placed),
    // and a consult or a take-over makes an agent Busy with no reservation at all. The state change into Busy names the
    // call, or the reservation that leads to it; anything that cannot be traced to a call that is over is left alone,
    // and so is an agent with any live interaction of their own.
    private async Task<int> RecoverOrphanedBusyAsync(CancellationToken cancellationToken)
    {
        var agents = await _agentManager.GetByPresenceAsync(AgentPresenceStatus.Busy, cancellationToken) ?? [];

        if (agents.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var latestChanges = (await _eventStore.GetLatestBeforeAsync(
            nameof(AgentProfile),
            [ContactCenterConstants.Events.AgentStateChanged],
            agents.Select(agent => agent.ItemId),
            now,
            cancellationToken) ?? [])
            .GroupBy(interactionEvent => interactionEvent.AggregateId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(interactionEvent => interactionEvent.OccurredUtc).Last(), StringComparer.Ordinal);
        var recovered = 0;

        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!latestChanges.TryGetValue(agent.ItemId, out var latestChange))
            {
                continue;
            }

            var interaction = await FindWorkThatMadeBusyAsync(latestChange.GetData<AgentStateChangedEventData>(), cancellationToken);

            if (interaction is null ||
                !interaction.IsSettled ||
                (interaction.EndedUtc ?? DateTime.MinValue) + _options.OrphanedBusyGracePeriod > now ||
                await _interactionManager.CountActiveByAgentAsync(agent.ItemId, cancellationToken) > 0)
            {
                continue;
            }

            AgentProfile updated;

            try
            {
                updated = await _presenceManager.CompleteWorkAsync(agent.ItemId, new AgentStateChangeContext
                {
                    Source = AgentStateChangeSources.Reconciled,
                    InteractionId = interaction.ItemId,
                }, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipped busy-state recovery for contended Contact Center agent '{AgentId}'.",
                    agent.ItemId.SanitizeLogValue());

                continue;
            }

            // The presence manager leaves an agent who still holds a reservation, or who is no longer Busy by the time
            // it looks, as they are. That used to be logged as a recovery all the same, so an agent still stuck Busy
            // behind a reservation read as put right in the log.
            if (updated is null)
            {
                _logger.LogWarning(
                    "Could not return Contact Center agent '{AgentId}' to work after the call '{InteractionId}' that made them Busy ended: they still hold a reservation, or are no longer Busy.",
                    agent.ItemId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue());

                continue;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Returned Contact Center agent '{AgentId}' to work: they were still Busy after the call '{InteractionId}' that made them Busy had ended.",
                    agent.ItemId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue());
            }

            recovered++;
        }

        return recovered;
    }

    // The call behind the agent's move into Busy: the one the change names, or the one the accepted reservation it
    // names leads to. Nothing is returned when the latest change is not into Busy, since the profile and its history
    // then disagree and there is nothing safe to act on.
    private async Task<Interaction> FindWorkThatMadeBusyAsync(AgentStateChangedEventData change, CancellationToken cancellationToken)
    {
        if (change is null || change.CurrentState != AgentPresenceStatus.Busy)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(change.InteractionId))
        {
            return await _interactionManager.FindByIdAsync(change.InteractionId, cancellationToken);
        }

        if (string.IsNullOrEmpty(change.ReservationId) || _reservationManager is null)
        {
            return null;
        }

        var reservation = await _reservationManager.FindByIdAsync(change.ReservationId, cancellationToken);

        return string.IsNullOrEmpty(reservation?.ActivityItemId)
            ? null
            : await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);
    }

    private async Task<int> RecoverWrapUpAsync(CancellationToken cancellationToken)
    {
        var agents = await _agentManager.GetByPresenceAsync(AgentPresenceStatus.WrapUp, cancellationToken);
        var recovered = 0;

        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var interactions = await _interactionManager.GetPendingWrapUpsByAgentAsync(agent.ItemId, cancellationToken);

            if (interactions.Any(interaction =>
                interaction.WrapUpStartedUtc.HasValue &&
                interaction.WrapUpStartedUtc.Value + _options.MaximumWrapUpDuration > _clock.UtcNow))
            {
                continue;
            }

            foreach (var interaction in interactions)
            {
                interaction.WrapUpCompletedUtc = _clock.UtcNow;
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }

            AgentProfile updated;

            try
            {
                // Wrap-up that ran past its limit is ended by the platform, which the audit keeps apart from an agent
                // finishing their work; wrap-up with no interaction left to finish is simply put right.
                updated = await _presenceManager.CompleteWorkAsync(agent.ItemId, new AgentStateChangeContext
                {
                    Source = interactions.Count > 0
                        ? AgentStateChangeSources.WrapUpTimedOut
                        : AgentStateChangeSources.Reconciled,
                    InteractionId = interactions.FirstOrDefault()?.ItemId,
                }, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipped availability recovery for contended Contact Center agent '{AgentId}'.",
                    agent.ItemId.SanitizeLogValue());

                continue;
            }

            if (updated is null)
            {
                continue;
            }

            recovered++;
            _logger.LogWarning(
                "Recovered expired or orphaned after-call work for Contact Center agent '{AgentId}'.",
                agent.ItemId.SanitizeLogValue());
        }

        return recovered;
    }
}
