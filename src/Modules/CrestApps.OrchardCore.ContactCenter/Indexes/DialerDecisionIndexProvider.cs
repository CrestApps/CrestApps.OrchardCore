using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps the <c>RoutingDecisionMade</c> and <c>DialSuppressed</c> events to the <see cref="DialerDecisionIndex"/> and
/// the <see cref="DialerDecisionAgentIndex"/>, so the dialer decision log is read through indexed columns rather than by
/// parsing every event's payload. Every other event maps to no row.
/// </summary>
/// <remarks>
/// The map runs whenever an event is saved, so it never throws: a payload it cannot read maps to no row instead of
/// failing the save of the event, and every value is cut to its column's length so a long value cannot be refused by a
/// database that enforces declared lengths.
/// </remarks>
public sealed class DialerDecisionIndexProvider : IndexProvider<InteractionEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerDecisionIndexProvider"/> class.
    /// </summary>
    public DialerDecisionIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<InteractionEvent> context)
    {
        context
            .For<DialerDecisionIndex>()
            .Map(interactionEvent => BuildDecision(interactionEvent));

        context
            .For<DialerDecisionAgentIndex>()
            .Map(interactionEvent => BuildAgents(interactionEvent));
    }

    /// <summary>
    /// Builds the decision row of a routing decision or dial suppression event.
    /// </summary>
    /// <param name="interactionEvent">The event.</param>
    /// <returns>The row, or <see langword="null"/> when the event is not a decision or its payload cannot be read.</returns>
    public static DialerDecisionIndex BuildDecision(InteractionEvent interactionEvent)
    {
        var decision = Read(interactionEvent);

        if (decision is null)
        {
            return null;
        }

        return new DialerDecisionIndex
        {
            ItemId = interactionEvent.ItemId,
            EventType = interactionEvent.EventType,
            QueueId = decision.QueueId,
            CampaignId = decision.CampaignId,
            ActivityItemId = decision.ActivityItemId,
            QueueItemId = decision.QueueItemId,
            AgentId = decision.AgentId,
            Succeeded = decision.Succeeded,
            Reason = decision.Reason,
            OccurrenceCount = decision.OccurrenceCount,
            OccurredUtc = interactionEvent.OccurredUtc,
            LastSeenUtc = decision.LastSeenUtc,
        };
    }

    /// <summary>
    /// Builds one row per agent a routing decision or dial suppression names: each candidate routing weighed and the
    /// agent it selected, or the agent reserved for a suppressed attempt.
    /// </summary>
    /// <param name="interactionEvent">The event.</param>
    /// <returns>The rows; none when the event is not a decision, names no agent, or its payload cannot be read.</returns>
    public static IEnumerable<DialerDecisionAgentIndex> BuildAgents(InteractionEvent interactionEvent)
    {
        var decision = Read(interactionEvent);

        if (decision is null)
        {
            return [];
        }

        return decision.Agents
            .Select(agent => new DialerDecisionAgentIndex
            {
                ItemId = interactionEvent.ItemId,
                AgentId = agent.AgentId,
                IsEligible = agent.IsEligible,
                IsSelected = agent.IsSelected,
                EventType = interactionEvent.EventType,
                QueueId = decision.QueueId,
                CampaignId = decision.CampaignId,
                ActivityItemId = decision.ActivityItemId,
                Succeeded = decision.Succeeded,
                Reason = decision.Reason,
                OccurrenceCount = decision.OccurrenceCount,
                OccurredUtc = interactionEvent.OccurredUtc,
                LastSeenUtc = decision.LastSeenUtc,
            })
            .ToArray();
    }

    private static Decision Read(InteractionEvent interactionEvent)
    {
        if (interactionEvent is null)
        {
            return null;
        }

        try
        {
            return interactionEvent.EventType switch
            {
                ContactCenterConstants.Events.RoutingDecisionMade => ReadRouting(interactionEvent),
                ContactCenterConstants.Events.DialSuppressed => ReadSuppression(interactionEvent),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Decision ReadRouting(InteractionEvent interactionEvent)
    {
        var data = interactionEvent.GetData<ActivityRoutingDecisionEventData>();

        if (data is null)
        {
            return null;
        }

        var agents = new List<DecisionAgent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in data.Candidates ?? [])
        {
            var agentId = Fit(candidate?.AgentId, DialerDecisionIndexColumns.IdLength);

            if (string.IsNullOrEmpty(agentId) || !seen.Add(agentId))
            {
                continue;
            }

            agents.Add(new DecisionAgent(
                agentId,
                candidate.IsEligible,
                string.Equals(candidate.AgentId, data.SelectedAgentId, StringComparison.Ordinal)));
        }

        var selectedAgentId = Fit(data.SelectedAgentId, DialerDecisionIndexColumns.IdLength);

        if (!string.IsNullOrEmpty(selectedAgentId) && seen.Add(selectedAgentId))
        {
            agents.Add(new DecisionAgent(selectedAgentId, IsEligible: true, IsSelected: true));
        }

        return new Decision
        {
            QueueId = Fit(data.QueueId, DialerDecisionIndexColumns.QueueIdLength),
            CampaignId = Fit(ContactCenterConstants.CampaignQueue.GetCampaignId(data.QueueId), DialerDecisionIndexColumns.IdLength),
            ActivityItemId = Fit(data.ActivityItemId, DialerDecisionIndexColumns.IdLength),
            QueueItemId = Fit(data.QueueItemId ?? interactionEvent.AggregateId, DialerDecisionIndexColumns.IdLength),
            AgentId = selectedAgentId,
            Succeeded = data.Succeeded,
            Reason = Fit(data.Reason, DialerDecisionIndexColumns.ReasonLength),
            OccurrenceCount = RoutingDecisionCoalescing.GetOccurrenceCount(data),
            LastSeenUtc = RoutingDecisionCoalescing.GetLastSeenUtc(interactionEvent, data),
            Agents = agents,
        };
    }

    private static Decision ReadSuppression(InteractionEvent interactionEvent)
    {
        var data = interactionEvent.GetData<DialerSuppressionEventData>();

        if (data is null)
        {
            return null;
        }

        var agentId = Fit(data.AgentId, DialerDecisionIndexColumns.IdLength);

        return new Decision
        {
            QueueId = Fit(data.QueueId, DialerDecisionIndexColumns.QueueIdLength),
            CampaignId = Fit(data.CampaignId ?? ContactCenterConstants.CampaignQueue.GetCampaignId(data.QueueId), DialerDecisionIndexColumns.IdLength),
            ActivityItemId = Fit(data.ActivityItemId ?? interactionEvent.AggregateId, DialerDecisionIndexColumns.IdLength),
            AgentId = agentId,
            Succeeded = false,
            Reason = data.Reason.ToString(),
            OccurrenceCount = 1,
            LastSeenUtc = interactionEvent.OccurredUtc,
            Agents = string.IsNullOrEmpty(agentId)
                ? []
                : [new DecisionAgent(agentId, IsEligible: true, IsSelected: true)],
        };
    }

    private static string Fit(string value, int length)
        => string.IsNullOrEmpty(value) || value.Length <= length
            ? value
            : value.Substring(0, length);

    private sealed class Decision
    {
        public string QueueId { get; init; }

        public string CampaignId { get; init; }

        public string ActivityItemId { get; init; }

        public string QueueItemId { get; init; }

        public string AgentId { get; init; }

        public bool Succeeded { get; init; }

        public string Reason { get; init; }

        public int OccurrenceCount { get; init; }

        public DateTime LastSeenUtc { get; init; }

        public IReadOnlyList<DecisionAgent> Agents { get; init; } = [];
    }

    private sealed record DecisionAgent(string AgentId, bool IsEligible, bool IsSelected);
}
