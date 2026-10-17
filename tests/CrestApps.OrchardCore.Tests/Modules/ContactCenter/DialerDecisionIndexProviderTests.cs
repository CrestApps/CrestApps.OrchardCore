using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The decision log's columns are read from the routing decision and dial suppression payloads once, when the event is
/// written, so a report never parses them again. The map runs on every event save, so it must never throw.
/// </summary>
public sealed class DialerDecisionIndexProviderTests
{
    private static readonly DateTime _occurredUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildDecision_OfACampaignRoutingDecision_IndexesItsQueueCampaignRecordOutcomeReasonAndCount()
    {
        // Arrange
        var queueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");
        var interactionEvent = Event(ContactCenterConstants.Events.RoutingDecisionMade, new ActivityRoutingDecisionEventData
        {
            QueueId = queueId,
            QueueItemId = "item-1",
            ActivityItemId = "act-1",
            Succeeded = false,
            Reason = "No agents are currently available for this queue.",
            OccurrenceCount = 12,
            LastSeenUtc = _occurredUtc.AddMinutes(11),
        });

        // Act
        var row = DialerDecisionIndexProvider.BuildDecision(interactionEvent);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("event-1", row.ItemId);
        Assert.Equal(ContactCenterConstants.Events.RoutingDecisionMade, row.EventType);
        Assert.Equal(queueId, row.QueueId);
        Assert.Equal("campaign-1", row.CampaignId);
        Assert.Equal("act-1", row.ActivityItemId);
        Assert.Equal("item-1", row.QueueItemId);
        Assert.False(row.Succeeded);
        Assert.Equal("No agents are currently available for this queue.", row.Reason);
        Assert.Equal(12, row.OccurrenceCount);
        Assert.Equal(_occurredUtc, row.OccurredUtc);
        Assert.Equal(_occurredUtc.AddMinutes(11), row.LastSeenUtc);
    }

    [Fact]
    public void BuildDecision_OfASuppression_IndexesTheReasonByName()
    {
        // Arrange
        var interactionEvent = Event(ContactCenterConstants.Events.DialSuppressed, new DialerSuppressionEventData
        {
            ActivityItemId = "act-1",
            CampaignId = "campaign-1",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Reason = DialerSuppressionReason.NationalDoNotCallRegistry,
            Destination = "+15550000001",
        });

        // Act
        var row = DialerDecisionIndexProvider.BuildDecision(interactionEvent);
        var agents = DialerDecisionIndexProvider.BuildAgents(interactionEvent).ToArray();

        // Assert
        Assert.Equal(nameof(DialerSuppressionReason.NationalDoNotCallRegistry), row.Reason);
        Assert.Equal("campaign-1", row.CampaignId);
        Assert.Equal("agent-1", row.AgentId);
        Assert.Equal(1, row.OccurrenceCount);
        Assert.Equal(_occurredUtc, row.LastSeenUtc);

        var agent = Assert.Single(agents);
        Assert.Equal("agent-1", agent.AgentId);
        Assert.True(agent.IsSelected);
    }

    [Fact]
    public void BuildAgents_OfARoutingDecision_WritesOneRowPerAgentItWeighed()
    {
        // Arrange
        var interactionEvent = Event(ContactCenterConstants.Events.RoutingDecisionMade, new ActivityRoutingDecisionEventData
        {
            QueueId = "queue-1",
            QueueItemId = "item-1",
            ActivityItemId = "act-1",
            Succeeded = true,
            SelectedAgentId = "agent-2",
            Reason = "Selected the highest-scoring eligible agent.",
            Candidates =
            [
                new ActivityRoutingCandidateDecisionData { AgentId = "agent-1", IsEligible = false, Reasons = ["Busy."] },
                new ActivityRoutingCandidateDecisionData { AgentId = "agent-2", IsEligible = true },
                new ActivityRoutingCandidateDecisionData { AgentId = "agent-1", IsEligible = false },
            ],
        });

        // Act
        var agents = DialerDecisionIndexProvider.BuildAgents(interactionEvent)
            .OrderBy(agent => agent.AgentId, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(["agent-1", "agent-2"], agents.Select(agent => agent.AgentId));
        Assert.False(agents[0].IsEligible);
        Assert.False(agents[0].IsSelected);
        Assert.True(agents[1].IsEligible);
        Assert.True(agents[1].IsSelected);
        Assert.All(agents, agent => Assert.Equal("act-1", agent.ActivityItemId));
    }

    [Fact]
    public void BuildDecision_OfAnotherEventOrAnUnreadablePayload_WritesNoRow()
    {
        // Arrange
        var other = Event(ContactCenterConstants.Events.QueueItemAdded, new { QueueId = "queue-1" });
        var unreadable = new InteractionEvent
        {
            ItemId = "event-2",
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            AggregateType = nameof(QueueItem),
            OccurredUtc = _occurredUtc,
            Data = "{ not json",
        };

        // Act
        var otherRow = DialerDecisionIndexProvider.BuildDecision(other);
        var unreadableRow = DialerDecisionIndexProvider.BuildDecision(unreadable);
        var unreadableAgents = DialerDecisionIndexProvider.BuildAgents(unreadable);

        // Assert
        Assert.Null(otherRow);
        Assert.Null(unreadableRow);
        Assert.Empty(unreadableAgents);
    }

    [Fact]
    public void BuildDecision_CutsAValueLongerThanItsColumn()
    {
        // Arrange
        var interactionEvent = Event(ContactCenterConstants.Events.RoutingDecisionMade, new ActivityRoutingDecisionEventData
        {
            QueueId = "queue-1",
            Reason = new string('x', 1000),
        });

        // Act
        var row = DialerDecisionIndexProvider.BuildDecision(interactionEvent);

        // Assert
        Assert.Equal(DialerDecisionIndexColumns.ReasonLength, row.Reason.Length);
    }

    private static InteractionEvent Event<T>(string eventType, T data)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = eventType,
            AggregateType = eventType == ContactCenterConstants.Events.DialSuppressed
                ? nameof(OmnichannelActivity)
                : nameof(QueueItem),
            AggregateId = "aggregate-1",
            OccurredUtc = _occurredUtc,
        };

        interactionEvent.SetData(data);

        return interactionEvent;
    }
}
