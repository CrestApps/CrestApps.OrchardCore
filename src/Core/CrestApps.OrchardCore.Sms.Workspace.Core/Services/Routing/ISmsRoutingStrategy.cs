namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;

/// <summary>
/// Selects the agent a routed (push) SMS conversation should be assigned to for a queue (department). This is
/// the channel-scoped seam a future channel-neutral work router would generalize; for now it encapsulates the
/// "pick an eligible member" decision so the inbound router and the reassignment sweep share one policy.
/// </summary>
public interface ISmsRoutingStrategy
{
    /// <summary>
    /// Selects the best eligible agent to receive a routed conversation on the specified queue, considering
    /// queue membership, SMS availability, and per-agent concurrency.
    /// </summary>
    /// <param name="queueId">The queue (department) the conversation is routed within.</param>
    /// <param name="excludeAgentId">An optional agent to exclude (for example the agent a reassignment is moving away from).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The selected agent profile id, or <see langword="null"/> when no eligible agent is available.</returns>
    Task<string> SelectAgentAsync(string queueId, string excludeAgentId = null, CancellationToken cancellationToken = default);
}
