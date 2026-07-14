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
/// Repairs inconsistent agent routing state after restarts, partial failures, or user-initiated resets so
/// stale queue assignments cannot keep blocking future inbound routing.
/// </summary>
public sealed class AgentWorkStateHealingService : IAgentWorkStateHealingService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityReservationService _reservationService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkStateHealingService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="serviceProvider">The service provider used to lazily resolve provider synchronization without a presence-manager cycle.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentWorkStateHealingService(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IActivityReservationService reservationService,
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<AgentWorkStateHealingService> logger)
    {
        _agentManager = agentManager;
        _reservationManager = reservationManager;
        _reservationService = reservationService;
        _queueItemManager = queueItemManager;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> HealForResetAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return 0;
        }

        return await HealAsync(
            agent,
            forceCancelReservation: true,
            releaseAssignedWork: true,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> HealForAvailabilityAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return 0;
        }

        return await HealAsync(
            agent,
            forceCancelReservation: false,
            releaseAssignedWork: agent.PresenceStatus == AgentPresenceStatus.Available,
            cancellationToken);
    }

    private async Task<int> HealAsync(
        AgentProfile agent,
        bool forceCancelReservation,
        bool releaseAssignedWork,
        CancellationToken cancellationToken)
    {
        var healed = 0;
        healed += await HealPendingReservationAsync(agent, forceCancelReservation, cancellationToken);
        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;
        healed += await HealActiveInteractionAsync(agent, releaseAssignedWork, cancellationToken);

        return healed;
    }

    private async Task<int> HealPendingReservationAsync(AgentProfile agent, bool forceCancel, CancellationToken cancellationToken)
    {
        var pendingReservation = !string.IsNullOrWhiteSpace(agent.ActiveReservationId)
            ? await _reservationManager.FindByIdAsync(agent.ActiveReservationId, cancellationToken)
            : await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken);

        if (pendingReservation is null)
        {
            if (string.IsNullOrWhiteSpace(agent.ActiveReservationId))
            {
                return 0;
            }

            agent.ActiveReservationId = null;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);

            return 1;
        }

        if (pendingReservation.Status != ReservationStatus.Pending)
        {
            if (!string.Equals(agent.ActiveReservationId, pendingReservation.ItemId, StringComparison.Ordinal))
            {
                return 0;
            }

            agent.ActiveReservationId = null;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);

            return 1;
        }

        var queueItem = await _queueItemManager.FindByIdAsync(pendingReservation.QueueItemId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(pendingReservation.ActivityItemId, cancellationToken);
        var interaction = await _interactionManager.FindByActivityIdAsync(pendingReservation.ActivityItemId, cancellationToken);
        var reservationExpired = pendingReservation.ExpiresUtc <= _clock.UtcNow;
        var queueItemInvalid = queueItem is null ||
            queueItem.Status != QueueItemStatus.Reserved ||
            !string.Equals(queueItem.ReservationId, pendingReservation.ItemId, StringComparison.Ordinal) ||
            !string.Equals(queueItem.AgentId, agent.ItemId, StringComparison.Ordinal);
        var activityInvalid = activity is null;
        var interactionInvalid =
            activity?.Source == ActivitySources.Inbound
                ? interaction is null ||
                interaction.Status != InteractionStatus.Ringing ||
                !string.Equals(interaction.AgentId, agent.ItemId, StringComparison.Ordinal)
                : interaction is not null &&
                (interaction.Status != InteractionStatus.Ringing ||
                    !string.Equals(interaction.AgentId, agent.ItemId, StringComparison.Ordinal));

        if (!forceCancel && !reservationExpired && !queueItemInvalid && !activityInvalid && !interactionInvalid)
        {
            return 0;
        }

        _logger.LogWarning(
            "Canceling stale pending reservation '{ReservationId}' for agent '{AgentId}'. ForceCancel={ForceCancel}, Expired={Expired}, QueueItemInvalid={QueueItemInvalid}, ActivityInvalid={ActivityInvalid}, InteractionInvalid={InteractionInvalid}.",
            OperationalLogRedactor.Pseudonymize(pendingReservation.ItemId, OperationalLogIdentifierCategory.Reservation),
            OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent),
            forceCancel,
            reservationExpired,
            queueItemInvalid,
            activityInvalid,
            interactionInvalid);

        await _reservationService.CancelAsync(pendingReservation.ItemId, cancellationToken);

        return 1;
    }

    private async Task<int> HealActiveInteractionAsync(AgentProfile agent, bool releaseAssignedWork, CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.FindActiveByAgentAsync(agent.ItemId, cancellationToken);

        if (interaction is null)
        {
            return 0;
        }

        var isProviderBacked = !string.IsNullOrWhiteSpace(interaction.ProviderName) &&
            !string.IsNullOrWhiteSpace(interaction.ProviderInteractionId);

        if (isProviderBacked)
        {
            var previousStatus = interaction.Status;
            var synchronizationService = _serviceProvider.GetRequiredService<IProviderCallStateSynchronizationService>();
            interaction = await synchronizationService.RefreshInteractionAsync(interaction, cancellationToken);

            if (interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
            {
                return interaction.Status == previousStatus ? 0 : 1;
            }
        }

        if (interaction.Status == InteractionStatus.Ringing)
        {
            if (isProviderBacked)
            {
                // Provider truth did not confirm this call ended, so it is either still ringing on the provider
                // or the provider was momentarily unreachable. Never requeue a provider-backed call from here:
                // requeuing would either yank a live ringing call away from the agent or resurrect a dead call
                // in an endless offer loop. The periodic provider-truth reconciliation releases it once the
                // provider confirms the call no longer exists.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Leaving provider-backed ringing interaction '{InteractionId}' for agent '{AgentId}' under provider control; reconciliation releases it once the provider confirms it ended.",
                        OperationalLogRedactor.Pseudonymize(interaction.ItemId, OperationalLogIdentifierCategory.Interaction),
                        OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));
                }

                return 0;
            }

            _logger.LogWarning(
                "Clearing stale ringing interaction '{InteractionId}' for agent '{AgentId}' because no active reservation remains.",
                OperationalLogRedactor.Pseudonymize(interaction.ItemId, OperationalLogIdentifierCategory.Interaction),
                OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));

            await RequeueInteractionAsync(interaction, cancellationToken);

            return 1;
        }

        if (!releaseAssignedWork ||
            interaction.Status is not (InteractionStatus.Connected or InteractionStatus.Held or InteractionStatus.Transferring or InteractionStatus.Conferenced) ||
            string.IsNullOrWhiteSpace(interaction.QueueId))
        {
            return 0;
        }

        if (isProviderBacked)
        {
            _logger.LogWarning(
                "Preserving provider-backed active interaction '{InteractionId}' for agent '{AgentId}' while the agent is being reset or marked available.",
                OperationalLogRedactor.Pseudonymize(interaction.ItemId, OperationalLogIdentifierCategory.Interaction),
                OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));

            return 0;
        }

        _logger.LogWarning(
            "Releasing stale assigned interaction '{InteractionId}' for agent '{AgentId}' because the agent is being reset or marked available while the interaction is still active.",
            OperationalLogRedactor.Pseudonymize(interaction.ItemId, OperationalLogIdentifierCategory.Interaction),
            OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent));

        await RequeueInteractionAsync(interaction, cancellationToken);

        return 1;
    }

    private async Task RequeueInteractionAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        var queueItem = await _queueItemManager.FindByActivityIdAsync(interaction.ActivityItemId, cancellationToken);

        if (queueItem is not null &&
            queueItem.Status is QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            queueItem.Status = QueueItemStatus.Waiting;
            queueItem.ReservationId = null;
            queueItem.AgentId = null;
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
        }

        interaction.Status = InteractionStatus.Created;
        interaction.AgentId = null;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var activity = await _activityManager.FindByIdAsync(interaction.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
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
}
