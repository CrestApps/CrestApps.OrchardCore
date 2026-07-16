using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
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
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedVoiceWorkOfferService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="agentWorkStateHealingServices">The optional agent state healers.</param>
    /// <param name="inboundVoiceService">The inbound voice service.</param>
    /// <param name="session">The YesSql session used to persist availability before querying routing indexes.</param>
    /// <param name="logger">The logger.</param>
    public QueuedVoiceWorkOfferService(
        IAgentProfileManager agentManager,
        IEnumerable<IAgentWorkStateHealingService> agentWorkStateHealingServices,
        IInboundVoiceService inboundVoiceService,
        ISession session,
        ILogger<QueuedVoiceWorkOfferService> logger)
    {
        _agentManager = agentManager;
        _agentWorkStateHealingService = agentWorkStateHealingServices.FirstOrDefault();
        _inboundVoiceService = inboundVoiceService;
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
        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available ||
            agent.QueueIds.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped queued voice offering because agent was missing, unavailable, or had no queue membership. AgentId={AgentId}, Presence={PresenceStatus}, QueueCount={QueueCount}.",
                    OperationalLogRedactor.Pseudonymize(agent?.ItemId, OperationalLogIdentifierCategory.Agent),
                    agent?.PresenceStatus,
                    agent?.QueueIds.Count ?? 0);
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
                    OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent),
                    OperationalLogRedactor.Pseudonymize(agent.ActiveReservationId, OperationalLogIdentifierCategory.Reservation));
            }

            return 0;
        }

        await _session.SaveChangesAsync(cancellationToken);

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
                        OperationalLogRedactor.Pseudonymize(queueId, OperationalLogIdentifierCategory.Queue),
                        OperationalLogRedactor.Pseudonymize(agent.ItemId, OperationalLogIdentifierCategory.Agent),
                        OperationalLogRedactor.Pseudonymize(agentUserId, OperationalLogIdentifierCategory.User));
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
}
