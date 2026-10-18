using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finds the work an agent is wrongly holding -- a call that is over but still on their line, a state that nothing backs --
/// and releases it, without ever ending a call the provider confirms is alive.
/// </summary>
public interface IStaleAgentWorkService
{
    /// <summary>
    /// Finds the stale work of the given agents. Provider answers about calls are reused for a short time, so a poll is
    /// cheap.
    /// </summary>
    /// <param name="agents">The agents to look at.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stale work by agent identifier; agents with none are not included.</returns>
    Task<IReadOnlyDictionary<string, StaleAgentWork>> DetectAsync(
        IReadOnlyCollection<AgentProfile> agents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an agent's stale work: settles their stale phone calls and interactions from what the provider says,
    /// releases their reservation and queue items, and returns them to their ready state.
    /// </summary>
    /// <remarks>
    /// The provider is asked again, with no reused answer. When it confirms that any call of the agent is still alive,
    /// nothing is released and the result says so.
    /// </remarks>
    /// <param name="agent">The agent.</param>
    /// <param name="actor">Who is releasing the agent, recorded on the state change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What was released, or why nothing was.</returns>
    Task<StaleAgentWorkRelease> ReleaseAsync(
        AgentProfile agent,
        ContactCenterActor actor,
        CancellationToken cancellationToken = default);
}
