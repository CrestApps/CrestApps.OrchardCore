using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Reads and updates an agent's availability for routed (push) SMS assignment. The state lives on the agent
/// profile's property bag, independent of voice presence.
/// </summary>
public interface ISmsAgentAvailabilityService
{
    /// <summary>
    /// Gets the SMS availability recorded on an agent profile, or the defaults when none is set.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <returns>The agent's SMS availability.</returns>
    SmsAgentAvailability Get(AgentProfile agent);

    /// <summary>
    /// Sets whether an agent is accepting routed SMS assignments and persists it.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="available">Whether the agent is accepting routed SMS.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated availability.</returns>
    Task<SmsAgentAvailability> SetAvailableAsync(AgentProfile agent, bool available, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the agent can actually be given a routed conversation right now. The stored flag says
    /// they volunteered; a live session heartbeat says they are still there to answer. Both are required,
    /// because the flag alone kept an agent who closed the browser available until the pickup sweep re-pooled
    /// each thread, and every message pushed at them in that window sat unanswered.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the agent is available for routed SMS.</returns>
    Task<bool> IsAvailableAsync(AgentProfile agent, CancellationToken cancellationToken = default);
}
