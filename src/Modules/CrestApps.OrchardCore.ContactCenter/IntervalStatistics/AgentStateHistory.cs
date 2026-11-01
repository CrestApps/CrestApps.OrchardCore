using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.IntervalStatistics;

/// <summary>
/// Reads agents' states from the agent state log with the calculator the workforce reports use.
/// </summary>
internal sealed class AgentStateHistory : IAgentStateHistory
{
    private readonly IInteractionEventStore _eventStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateHistory"/> class.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="clock">The clock.</param>
    public AgentStateHistory(IInteractionEventStore eventStore, IClock clock)
    {
        _eventStore = eventStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentStateSpan>> GetSpansAsync(IEnumerable<string> agentIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        var ids = agentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var now = _clock.UtcNow;
        var end = toUtc < now ? toUtc : now;

        if (ids.Length == 0 || end <= fromUtc)
        {
            return [];
        }

        var stretch = await AgentDailyTimeCalculator.ReadAsync(_eventStore, ids, fromUtc, end, end.AddTicks(-1), lookbehind: true, onlyTheseAgents: true, cancellationToken);

        return [.. stretch.Intervals
            .Where(interval => interval.EndUtc > interval.StartUtc)
            .OrderBy(interval => interval.AgentId, StringComparer.Ordinal)
            .ThenBy(interval => interval.StartUtc)
            .Select(interval => new AgentStateSpan
            {
                AgentId = interval.AgentId,
                Status = interval.Status,
                Reason = interval.Reason,
                StartUtc = interval.StartUtc,
                EndUtc = interval.EndUtc,
            })];
    }
}
