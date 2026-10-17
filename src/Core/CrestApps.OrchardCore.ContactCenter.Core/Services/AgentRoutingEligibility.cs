using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The rules of the availability gate an agent passes before routing considers them for a queue: Available, entitled
/// to and signed in to the queue, a live session that carries the queue, and spare capacity.
/// </summary>
/// <remarks>
/// <see cref="AgentAvailabilityService"/> decides availability with these rules, and the agent availability report
/// explains an agent with the same rules, so the reason a supervisor reads is the reason routing acted on.
/// </remarks>
internal static class AgentRoutingEligibility
{
    /// <summary>
    /// Determines whether a session is live: online, with a connection, and with a heartbeat no older than the
    /// heartbeat timeout allows.
    /// </summary>
    /// <param name="session">The agent's session, or <see langword="null"/> when the agent has none.</param>
    /// <param name="liveAfterUtc">The oldest heartbeat that still counts as live.</param>
    /// <returns><see langword="true"/> when the session is live; otherwise <see langword="false"/>.</returns>
    public static bool IsSessionLive(AgentSession session, DateTime liveAfterUtc)
        => session is not null &&
            session.IsOnline &&
            session.ConnectionIds is { Count: > 0 } &&
            session.LastHeartbeatUtc.HasValue &&
            session.LastHeartbeatUtc.Value >= liveAfterUtc;

    /// <summary>
    /// Gets the reasons the agent's profile keeps them out of a queue's routing: their presence, their entitlement and
    /// their sign-in.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <returns>The reasons that apply, or <see cref="AgentRoutingIneligibility.None"/>.</returns>
    public static AgentRoutingIneligibility GetProfileIneligibility(AgentProfile agent, string queueId)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var result = AgentRoutingIneligibility.None;

        if (agent.PresenceStatus != AgentPresenceStatus.Available)
        {
            result |= AgentRoutingIneligibility.NotAvailable;
        }

        if (string.IsNullOrEmpty(queueId) || agent.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) != true)
        {
            result |= AgentRoutingIneligibility.NotEntitled;
        }

        if (string.IsNullOrEmpty(queueId) || agent.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) != true)
        {
            result |= AgentRoutingIneligibility.NotSignedIn;
        }

        return result;
    }

    /// <summary>
    /// Gets the reasons the agent's session keeps them out of a queue's routing.
    /// </summary>
    /// <param name="session">The agent's session, or <see langword="null"/> when the agent has none.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="liveAfterUtc">The oldest heartbeat that still counts as live.</param>
    /// <returns>The reasons that apply, or <see cref="AgentRoutingIneligibility.None"/>.</returns>
    public static AgentRoutingIneligibility GetSessionIneligibility(AgentSession session, string queueId, DateTime liveAfterUtc)
    {
        if (session is null)
        {
            return AgentRoutingIneligibility.NoLiveSession;
        }

        var result = AgentRoutingIneligibility.None;

        if (!IsSessionLive(session, liveAfterUtc))
        {
            result |= AgentRoutingIneligibility.NoLiveSession;
        }

        if (string.IsNullOrEmpty(queueId) || session.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) != true)
        {
            result |= AgentRoutingIneligibility.SessionNotSignedIn;
        }

        return result;
    }

    /// <summary>
    /// Determines whether the agent already handles as many interactions as they may handle at once. An agent allowed
    /// no interactions is allowed one.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="activeInteractionCount">How many interactions the agent handles now.</param>
    /// <returns><see langword="true"/> when the agent is at capacity; otherwise <see langword="false"/>.</returns>
    public static bool IsAtCapacity(AgentProfile agent, int activeInteractionCount)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return activeInteractionCount >= GetCapacity(agent);
    }

    /// <summary>
    /// Gets how many interactions the agent may handle at once. An agent allowed no interactions is allowed one.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <returns>The agent's capacity.</returns>
    public static int GetCapacity(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return Math.Max(1, agent.MaxConcurrentInteractions);
    }

    /// <summary>
    /// Gets every reason the availability gate keeps the agent out of a queue's routing.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="session">The agent's session, or <see langword="null"/> when the agent has none.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="activeInteractionCount">How many interactions the agent handles now.</param>
    /// <param name="liveAfterUtc">The oldest heartbeat that still counts as live.</param>
    /// <returns>The reasons that apply, or <see cref="AgentRoutingIneligibility.None"/> when the gate lets the agent through.</returns>
    public static AgentRoutingIneligibility Evaluate(
        AgentProfile agent,
        AgentSession session,
        string queueId,
        int activeInteractionCount,
        DateTime liveAfterUtc)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var result = GetProfileIneligibility(agent, queueId) | GetSessionIneligibility(session, queueId, liveAfterUtc);

        if (IsAtCapacity(agent, activeInteractionCount))
        {
            result |= AgentRoutingIneligibility.AtCapacity;
        }

        return result;
    }
}
