using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Reads agents' states over a stretch of time from the agent state log, the way the Contact Center workforce reports
/// read them.
/// </summary>
public interface IAgentStateHistory
{
    /// <summary>
    /// Gets the agents' state spans over a stretch of time, cut to the stretch.
    /// </summary>
    /// <param name="agentIds">The agent profiles.</param>
    /// <param name="fromUtc">The start of the stretch.</param>
    /// <param name="toUtc">The end of the stretch; spans end no later than now.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The spans, by agent and in order.</returns>
    Task<IReadOnlyList<AgentStateSpan>> GetSpansAsync(IEnumerable<string> agentIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// A stretch of time an agent spent in one state.
/// </summary>
public sealed class AgentStateSpan
{
    /// <summary>
    /// Gets or sets the agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the state's reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }
}
