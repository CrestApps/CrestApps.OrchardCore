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
    private readonly AgentAvailabilityOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityRecoveryService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="options">The availability policy.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentAvailabilityRecoveryService(
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IAgentPresenceManager presenceManager,
        IActivityReservationManager reservationManager,
        IOptions<AgentAvailabilityOptions> options,
        IClock clock,
        ILogger<AgentAvailabilityRecoveryService> logger)
    {
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _presenceManager = presenceManager;
        _reservationManager = reservationManager;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
        => await RecoverWrapUpAsync(cancellationToken) + await RecoverOrphanedBusyAsync(cancellationToken);

    // An agent is Busy from the moment they accept an offer until the call's end releases them. When that release is
    // missed -- live, a callback whose agent leg failed left the agent Busy with nothing on the line, and a supervisor's
    // Set Available waited for work that would never end -- the agent is returned to work once every call they accepted
    // is over and the grace period has passed. An accepted offer with no call yet (a preview dial or callback still being
    // placed), a live call, and Busy with no offer of their own (a consult, a call taken over) are all left alone.
    private async Task<int> RecoverOrphanedBusyAsync(CancellationToken cancellationToken)
    {
        var agents = await _agentManager.GetByPresenceAsync(AgentPresenceStatus.Busy, cancellationToken) ?? [];
        var recovered = 0;

        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accepted = (await _reservationManager.GetActiveByAgentAsync(agent.ItemId, cancellationToken) ?? [])
                .Where(reservation => reservation.Status == ReservationStatus.Accepted)
                .ToList();

            if (accepted.Count == 0)
            {
                continue;
            }

            Interaction lastEnded = null;
            var stillWorking = false;

            foreach (var reservation in accepted)
            {
                var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

                if (interaction is null || !interaction.IsSettled)
                {
                    stillWorking = true;

                    break;
                }

                if (lastEnded is null || (interaction.EndedUtc ?? DateTime.MinValue) > (lastEnded.EndedUtc ?? DateTime.MinValue))
                {
                    lastEnded = interaction;
                }
            }

            if (stillWorking || lastEnded is null || (lastEnded.EndedUtc ?? DateTime.MinValue) + _options.OrphanedBusyGracePeriod > _clock.UtcNow)
            {
                continue;
            }

            try
            {
                await _presenceManager.CompleteWorkAsync(agent.ItemId, new AgentStateChangeContext
                {
                    Source = AgentStateChangeSources.Reconciled,
                    InteractionId = lastEnded.ItemId,
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

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Returned Contact Center agent '{AgentId}' to work: they were Busy after the call '{InteractionId}' they accepted had ended.",
                    agent.ItemId.SanitizeLogValue(),
                    lastEnded.ItemId.SanitizeLogValue());
            }

            recovered++;
        }

        return recovered;
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
