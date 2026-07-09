using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Offers already-waiting inbound voice work to an agent who has just become reachable again.
/// </summary>
public sealed class QueuedVoiceWorkOfferService : IQueuedVoiceWorkOfferService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentWorkStateHealingService _agentWorkStateHealingService;
    private readonly IInboundVoiceService _inboundVoiceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedVoiceWorkOfferService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="agentWorkStateHealingServices">The optional agent state healers.</param>
    /// <param name="inboundVoiceService">The inbound voice service.</param>
    public QueuedVoiceWorkOfferService(
        IAgentProfileManager agentManager,
        IEnumerable<IAgentWorkStateHealingService> agentWorkStateHealingServices,
        IInboundVoiceService inboundVoiceService)
    {
        _agentManager = agentManager;
        _agentWorkStateHealingService = agentWorkStateHealingServices.FirstOrDefault();
        _inboundVoiceService = inboundVoiceService;
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
        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available ||
            agent.QueueIds.Count == 0)
        {
            return 0;
        }

        if (_agentWorkStateHealingService is not null)
        {
            await _agentWorkStateHealingService.HealForAvailabilityAsync(agent.ItemId, cancellationToken);
            agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;
        }

        if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId))
        {
            return 0;
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
}
