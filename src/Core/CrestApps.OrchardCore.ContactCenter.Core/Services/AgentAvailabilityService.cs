using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Computes routing availability from administrative presence, live sessions, queue opt-in, and capacity.
/// </summary>
public sealed class AgentAvailabilityService : IAgentAvailabilityService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentSessionManager _sessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly AgentAvailabilityOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="sessionManager">The live agent session manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="options">The availability policy.</param>
    /// <param name="clock">The clock.</param>
    public AgentAvailabilityService(
        IAgentProfileManager agentManager,
        IAgentSessionManager sessionManager,
        IInteractionManager interactionManager,
        IOptions<AgentAvailabilityOptions> options,
        IClock clock)
    {
        _agentManager = agentManager;
        _sessionManager = sessionManager;
        _interactionManager = interactionManager;
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<AgentAvailability> GetAsync(
        string agentId,
        string queueId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        // The gate's rules live in AgentRoutingEligibility, which the agent availability report explains an agent with,
        // so what a supervisor reads there is what is decided here.
        if (agent is null ||
            AgentRoutingEligibility.GetProfileIneligibility(agent, queueId) != AgentRoutingIneligibility.None)
        {
            return null;
        }

        var session = await _sessionManager.FindByUserIdAsync(agent.UserId, cancellationToken);
        var liveAfter = _clock.UtcNow - _options.HeartbeatTimeout;

        if (AgentRoutingEligibility.GetSessionIneligibility(session, queueId, liveAfter) != AgentRoutingIneligibility.None)
        {
            return null;
        }

        var activeInteractionCount = await _interactionManager.CountActiveByAgentAsync(agentId, cancellationToken);

        if (AgentRoutingEligibility.IsAtCapacity(agent, activeInteractionCount))
        {
            return null;
        }

        return new AgentAvailability
        {
            Agent = agent,
            LastHeartbeatUtc = session.LastHeartbeatUtc.Value,
            ActiveInteractionCount = activeInteractionCount,
        };
    }

    /// <inheritdoc/>
    public async Task<AgentAvailability> GetForDirectAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        // A direct-to-agent (personal line) offer deliberately skips queue entitlement and queue sign-in: the
        // agent is named on the entry point, so the only questions are whether they are present and set to
        // Available, have a live session, and are within capacity.
        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available)
        {
            return null;
        }

        var session = await _sessionManager.FindByUserIdAsync(agent.UserId, cancellationToken);
        var liveAfter = _clock.UtcNow - _options.HeartbeatTimeout;

        if (!AgentRoutingEligibility.IsSessionLive(session, liveAfter))
        {
            return null;
        }

        var activeInteractionCount = await _interactionManager.CountActiveByAgentAsync(agentId, cancellationToken);

        if (AgentRoutingEligibility.IsAtCapacity(agent, activeInteractionCount))
        {
            return null;
        }

        return new AgentAvailability
        {
            Agent = agent,
            LastHeartbeatUtc = session.LastHeartbeatUtc.Value,
            ActiveInteractionCount = activeInteractionCount,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentAvailability>> GetForQueueAsync(
        string queueId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var agents = await _agentManager.GetAvailableForQueueAsync(queueId, cancellationToken);

        if (agents.Count == 0)
        {
            return [];
        }

        var agentsByUserId = agents
            .Where(agent => !string.IsNullOrWhiteSpace(agent.UserId))
            .ToDictionary(agent => agent.UserId, StringComparer.Ordinal);
        var sessions = await _sessionManager.GetByUserIdsAsync(agentsByUserId.Keys.ToArray(), cancellationToken);
        var activeCounts = await _interactionManager.CountActiveByAgentIdsAsync(
            agents.Select(agent => agent.ItemId).ToArray(),
            cancellationToken);
        var liveAfter = _clock.UtcNow - _options.HeartbeatTimeout;
        var available = new List<AgentAvailability>(agents.Count);

        foreach (var session in sessions)
        {
            if (AgentRoutingEligibility.GetSessionIneligibility(session, queueId, liveAfter) != AgentRoutingIneligibility.None ||
                !agentsByUserId.TryGetValue(session.UserId, out var agent))
            {
                continue;
            }

            activeCounts.TryGetValue(agent.ItemId, out var activeInteractionCount);

            if (AgentRoutingEligibility.IsAtCapacity(agent, activeInteractionCount))
            {
                continue;
            }

            available.Add(new AgentAvailability
            {
                Agent = agent,
                LastHeartbeatUtc = session.LastHeartbeatUtc.Value,
                ActiveInteractionCount = activeInteractionCount,
            });
        }

        return available;
    }
}
