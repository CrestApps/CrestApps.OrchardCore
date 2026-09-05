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
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue identifier, or <see langword="null"/> when no queue has eligible work.</returns>
    Task<string> SelectNextForAgentAsync(AgentProfile agent, CancellationToken cancellationToken = default);
}
