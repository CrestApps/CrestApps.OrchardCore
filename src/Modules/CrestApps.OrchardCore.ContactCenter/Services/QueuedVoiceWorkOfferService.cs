using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Offers already-waiting inbound voice work to an agent who has just become reachable again.
/// </summary>
public sealed class QueuedVoiceWorkOfferService : IQueuedVoiceWorkOfferService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentWorkStateHealingService _agentWorkStateHealingService;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IInteractionManager _interactionManager;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedVoiceWorkOfferService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="agentWorkStateHealingServices">The optional agent state healers.</param>
    /// <param name="inboundVoiceService">The inbound voice service.</param>
    /// <param name="queueItemManager">The queue item manager used to find held direct-to-agent calls.</param>
    /// <param name="interactionManager">The interaction manager used to resolve a held call's direct target.</param>
    /// <param name="session">The YesSql session used to persist availability before querying routing indexes.</param>
    /// <param name="logger">The logger.</param>
    public QueuedVoiceWorkOfferService(
        IAgentProfileManager agentManager,
        IEnumerable<IAgentWorkStateHealingService> agentWorkStateHealingServices,
        IInboundVoiceService inboundVoiceService,
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        ISession session,
        ILogger<QueuedVoiceWorkOfferService> logger)
    {
        _agentManager = agentManager;
        _agentWorkStateHealingService = agentWorkStateHealingServices.FirstOrDefault();
        _inboundVoiceService = inboundVoiceService;
        _queueItemManager = queueItemManager;
        _interactionManager = interactionManager;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> OfferForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        return await OfferForProfileAsync(agent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> OfferForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var agent = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        return await OfferForProfileAsync(agent, cancellationToken);
    }

    private async Task<int> OfferForProfileAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        // Queue membership is not required: a direct-to-agent (personal line) agent may belong to no queue yet
        // still have a call held for them. Only presence gates whether any waiting work can be offered.
        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped queued voice offering because agent was missing or unavailable. AgentId={AgentId}, Presence={PresenceStatus}.",
                    agent?.ItemId.SanitizeLogValue(),
                    agent?.PresenceStatus);
            }

            return 0;
        }

        if (_agentWorkStateHealingService is not null)
        {
            await _agentWorkStateHealingService.HealForAvailabilityAsync(agent.ItemId, cancellationToken);
            agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;
        }

        if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped queued voice offering for agent '{AgentId}' because reservation '{ReservationId}' is active.",
                    agent.ItemId.SanitizeLogValue(),
                    agent.ActiveReservationId.SanitizeLogValue());
            }

            return 0;
        }

        await _session.SaveChangesAsync(cancellationToken);

        // A held direct-to-agent (personal line) call takes precedence: it is a caller already waiting
        // specifically for this agent, and it does not depend on any queue membership. Offering one reserves
        // the agent, so when that succeeds we are done and must not also pull from queues.
        if (await OfferHeldDirectCallsAsync(agent, cancellationToken) > 0)
        {
            return 1;
        }

        var offered = 0;

        foreach (var queueId in agent.QueueIds
            .Where(queueId => !string.IsNullOrWhiteSpace(queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var agentUserId = await _inboundVoiceService.OfferNextAsync(queueId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(agentUserId))
            {
                offered++;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Offered the next queued voice activity from queue '{QueueId}' to agent '{AgentId}' for user '{UserId}'.",
                        queueId.SanitizeLogValue(),
                        agent.ItemId.SanitizeLogValue(),
                        agentUserId.SanitizeLogValue());
                }
            }

            agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken);

            if (agent is null ||
                agent.PresenceStatus != AgentPresenceStatus.Available ||
                !string.IsNullOrWhiteSpace(agent.ActiveReservationId))
            {
                break;
            }
        }

        return offered;
    }

    // Offers a call that was held for this specific agent while they were unavailable. Held direct calls wait
    // under the synthetic direct-routing queue tagged with their target agent; this connects the longest-held
    // caller for the agent as soon as the agent becomes available. The agent can take one call at a time, so at
    // most one held call is offered here.
    private async Task<int> OfferHeldDirectCallsAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        var waiting = await _queueItemManager.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        foreach (var item in waiting.OrderBy(queueItem => queueItem.EnqueuedUtc))
        {
            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

            if (interaction is null ||
                !interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.TargetAgentMetadataKey, out var target) ||
                !string.Equals(target?.ToString(), agent.ItemId, StringComparison.Ordinal))
            {
                continue;
            }

            var offeredUserId = await _inboundVoiceService.OfferToAgentAsync(
                item.ActivityItemId,
                ContactCenterConstants.DirectRouting.QueueId,
                agent.ItemId,
                ReadRingTimeoutSeconds(interaction),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(offeredUserId))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Offered a held direct-to-agent call for activity '{ActivityItemId}' to agent '{AgentId}' after they became available.",
                        item.ActivityItemId.SanitizeLogValue(),
                        agent.ItemId.SanitizeLogValue());
                }

                return 1;
            }
        }

        return 0;
    }

    private static int? ReadRingTimeoutSeconds(Interaction interaction)
    {
        if (interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey, out var value) &&
            value is not null &&
            int.TryParse(value.ToString(), out var seconds) &&
            seconds > 0)
        {
            return seconds;
        }

        return null;
    }
}
