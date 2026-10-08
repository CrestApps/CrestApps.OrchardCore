using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Chooses which of an agent's queues to serve next. Reservation still happens through
/// <c>AssignNextAsync(queueId)</c>, so the locking semantics are unchanged; what this adds is deciding which
/// queue that call is made against, instead of walking the agent's stored list in order.
/// </summary>
public interface IAgentWorkSelector
{
    /// <summary>
    /// Selects the queue holding the contact this agent should be offered next.
    /// </summary>
    /// <param name="agent">The agent to select work for.</param>
    /// <param name="excludedQueueIds">
    /// Queues the caller has already ruled out for this pass (for example a paced campaign queue the dialer
    /// owns, or a queue whose head item this agent could not take). They are skipped so the selection moves on
    /// to the next-best queue instead of returning the same answer again.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue identifier, or <see langword="null"/> when no queue has eligible work.</returns>
    Task<string> SelectNextForAgentAsync(
        AgentProfile agent,
        IReadOnlyCollection<string> excludedQueueIds = null,
        CancellationToken cancellationToken = default);
}
