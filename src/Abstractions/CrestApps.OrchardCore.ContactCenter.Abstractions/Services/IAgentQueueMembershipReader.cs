namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Reads which queues an agent serves, and which agents serve a queue, without exposing the Contact Center
/// routing types. Channels that reuse queues only as a grouping of agents (the SMS Portal, for example) depend
/// on this contract so they work on a tenant that has the agent directory but not the Work Distribution feature.
/// It is registered by the Agent Services feature, which owns the agent directory.
/// </summary>
public interface IAgentQueueMembershipReader
{
    /// <summary>
    /// Lists the identifiers of the queues the agent serves, after the tenant's entitlement policy is applied.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue identifiers, or an empty collection when the agent serves none.</returns>
    Task<IReadOnlyCollection<string>> GetQueueIdsForAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the agent serves the queue, after the tenant's entitlement policy is applied.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the agent serves the queue.</returns>
    Task<bool> IsMemberAsync(string agentId, string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the identifiers of the agents that serve the queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent profile identifiers, or an empty collection when the queue has no members.</returns>
    Task<IReadOnlyCollection<string>> GetAgentIdsForQueueAsync(string queueId, CancellationToken cancellationToken = default);
}
