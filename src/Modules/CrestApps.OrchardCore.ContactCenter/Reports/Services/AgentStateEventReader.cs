using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Reads what a period's account of agent time needs from the event log: each agent's last state change before the
/// period, so the period opens in the state the agent was already in, and the period's own events.
/// </summary>
internal static class AgentStateEventReader
{
    /// <summary>
    /// Reads the agents' state events for a period.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentIds">The agents whose state before the period is looked up.</param>
    /// <param name="onlyTheseAgents">Whether the period's events are limited to <paramref name="agentIds"/>. When
    /// <see langword="false"/>, the period's events of every agent are read, and each one's earlier state with them.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="additionalEventTypes">Further agent event types to read within the period, such as connection
    /// events for a timeline.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The earlier state changes followed by the period's events.</returns>
    public static async Task<IReadOnlyList<InteractionEvent>> ReadAsync(
        IInteractionEventStore eventStore,
        IEnumerable<string> agentIds,
        bool onlyTheseAgents,
        DateTime fromUtc,
        DateTime toUtc,
        IEnumerable<string> additionalEventTypes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        var ids = (agentIds ?? [])
            .Where(agentId => !string.IsNullOrEmpty(agentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var windowTypes = AgentStateTimeline.StateEventTypes
            .Concat(additionalEventTypes ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Reading through the store rather than querying the index directly brings a payload written by an earlier
        // release to the schema this release understands.
        var window = await eventStore.GetByAggregateWindowAsync(
            nameof(AgentProfile),
            windowTypes,
            onlyTheseAgents ? ids : null,
            fromUtc,
            toUtc,
            cancellationToken);

        var anchorIds = ids
            .Concat(window.Select(interactionEvent => interactionEvent.AggregateId))
            .Where(agentId => !string.IsNullOrEmpty(agentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var anchors = await eventStore.GetLatestBeforeAsync(
            nameof(AgentProfile),
            AgentStateTimeline.StateEventTypes,
            anchorIds,
            fromUtc,
            cancellationToken);

        return [.. anchors, .. window];
    }
}
