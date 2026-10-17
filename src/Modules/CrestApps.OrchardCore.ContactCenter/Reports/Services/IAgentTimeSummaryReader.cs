using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Reads every agent's time over a period for the time-based workforce reports: whole UTC days inside the period from
/// their daily summaries, and the rest of the period from the agent state log.
/// </summary>
internal interface IAgentTimeSummaryReader
{
    /// <summary>
    /// Reads every agent's time over a period, by state, reason, queues and campaigns.
    /// </summary>
    /// <param name="agentIds">The agents whose state before the period is looked up, as a report over the log does.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: an agent still in a state then is counted up to it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// Intervals that add up, per agent and per state, reason, queue set and campaign set, to the same time and the same
    /// number of intervals a report over the log finds. Their start and end only carry their length.
    /// </returns>
    Task<IReadOnlyList<AgentPresenceInterval>> ReadAsync(
        IReadOnlyCollection<string> agentIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
