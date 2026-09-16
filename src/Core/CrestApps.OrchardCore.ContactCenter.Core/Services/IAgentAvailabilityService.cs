using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Computes the canonical routing availability of Contact Center agents.
/// </summary>
public interface IAgentAvailabilityService
{
    /// <summary>
    /// Gets the canonical availability of the specified agent for a queue.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The availability projection, or <see langword="null"/> when the agent is not eligible.</returns>
    Task<AgentAvailability> GetAsync(
        string agentId,
        string queueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the canonical availability of the specified agent for a direct-to-agent (personal line) offer.
    /// Unlike <see cref="GetAsync"/> this does not require the agent to be entitled to, or signed into, any
    /// queue: a specific-agent entry point rings the person directly. The agent must still be present and set
    /// to <see cref="AgentPresenceStatus.Available"/>, have a live session, and be within capacity.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The availability projection, or <see langword="null"/> when the agent cannot take a direct call.</returns>
    Task<AgentAvailability> GetForDirectAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists agents that are entitled, opted in, live, available, and within capacity for the specified queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The canonical availability projections for eligible agents.</returns>
    Task<IReadOnlyCollection<AgentAvailability>> GetForQueueAsync(
        string queueId,
        CancellationToken cancellationToken = default);
}
