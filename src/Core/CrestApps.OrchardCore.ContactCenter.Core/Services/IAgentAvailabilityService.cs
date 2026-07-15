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
    /// Lists agents that are entitled, opted in, live, available, and within capacity for the specified queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The canonical availability projections for eligible agents.</returns>
    Task<IReadOnlyCollection<AgentAvailability>> ListForQueueAsync(
        string queueId,
        CancellationToken cancellationToken = default);
}
