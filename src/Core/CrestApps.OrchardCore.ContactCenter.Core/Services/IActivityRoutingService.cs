using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Selects an eligible agent for a queued activity by applying routing strategies.
/// </summary>
public interface IActivityRoutingService
{
    /// <summary>
    /// Selects the agent that should receive the queued item.
    /// </summary>
    /// <param name="queue">The queue being routed.</param>
    /// <param name="queueItem">The queued activity.</param>
    /// <param name="agents">The available agents signed in to the queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The explainable routing decision.</returns>
    Task<ActivityRoutingDecision> SelectAgentAsync(
        ActivityQueue queue,
        QueueItem queueItem,
        IEnumerable<AgentProfile> agents,
        CancellationToken cancellationToken = default);
}
