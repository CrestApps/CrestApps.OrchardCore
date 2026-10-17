using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The decision log report counts the period's decisions by reason. A routing event that stands for many identical
/// decisions is counted by the decisions, so a queue that went unanswered for an hour reads as sixty decisions, not one.
/// </summary>
public sealed class DialerDecisionReasonSummaryTests
{
    private static readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Summarize_GroupsByKindAndReason_CountingEveryDecisionAnEventStandsFor_MostFrequentFirst()
    {
        // Arrange
        DialerDecisionIndex[] entries =
        [
            Entry(ContactCenterConstants.Events.RoutingDecisionMade, "No agents are currently available for this queue.", occurrences: 60, _start.AddMinutes(59)),
            Entry(ContactCenterConstants.Events.RoutingDecisionMade, "No agents are currently available for this queue.", occurrences: 5, _start.AddMinutes(70)),
            Entry(ContactCenterConstants.Events.DialSuppressed, nameof(DialerSuppressionReason.DoNotCall), occurrences: 1, _start.AddMinutes(3)),
            Entry(ContactCenterConstants.Events.DialSuppressed, nameof(DialerSuppressionReason.DoNotCall), occurrences: 1, _start.AddMinutes(4)),
            Entry(ContactCenterConstants.Events.RoutingDecisionMade, "Selected the highest-scoring eligible agent.", occurrences: 0, _start.AddMinutes(1)),
        ];

        // Act
        var rows = DialerDecisionReasonSummary.Summarize(entries);

        // Assert
        Assert.Equal(3, rows.Count);

        Assert.Equal(ContactCenterConstants.Events.RoutingDecisionMade, rows[0].EventType);
        Assert.Equal("No agents are currently available for this queue.", rows[0].Reason);
        Assert.Equal(65, rows[0].Decisions);
        Assert.Equal(2, rows[0].Events);
        Assert.Equal(_start.AddMinutes(70), rows[0].LastSeenUtc);

        Assert.Equal(nameof(DialerSuppressionReason.DoNotCall), rows[1].Reason);
        Assert.Equal(2, rows[1].Decisions);

        // An entry written before decisions were coalesced counts as one decision.
        Assert.Equal("Selected the highest-scoring eligible agent.", rows[2].Reason);
        Assert.Equal(1, rows[2].Decisions);
    }

    [Fact]
    public void Describe_OfARoutingDecision_ListsEachCandidateWithWhyItWasTurnedAway()
    {
        // Arrange
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            AggregateType = nameof(QueueItem),
            OccurredUtc = _start,
        };

        interactionEvent.SetData(new ActivityRoutingDecisionEventData
        {
            QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
            ActivityItemId = "act-1",
            Succeeded = false,
            Reason = "No available agent matched the queue routing policy.",
            OccurrenceCount = 3,
            LastSeenUtc = _start.AddMinutes(2),
            Candidates =
            [
                new ActivityRoutingCandidateDecisionData { AgentId = "agent-1", IsEligible = false, Reasons = ["Lacks the required skill 'Spanish'."] },
            ],
        });

        // Act
        var row = DialerDecisionLogReportProvider.Describe(interactionEvent);

        // Assert
        Assert.Equal("campaign-1", row.CampaignId);
        Assert.Equal("act-1", row.ActivityItemId);
        Assert.Equal(3, row.Occurrences);
        Assert.Equal(_start.AddMinutes(2), row.LastSeenUtc);
        Assert.False(row.Suppressed);

        var candidate = Assert.Single(row.Candidates);
        Assert.Equal("agent-1", candidate.AgentId);
        Assert.Equal(["Lacks the required skill 'Spanish'."], candidate.Reasons);
    }

    [Fact]
    public void Describe_OfASuppression_CarriesTheReasonAndTheDestination()
    {
        // Arrange
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = ContactCenterConstants.Events.DialSuppressed,
            AggregateType = nameof(OmnichannelActivity),
            AggregateId = "act-1",
            OccurredUtc = _start,
        };

        interactionEvent.SetData(new DialerSuppressionEventData
        {
            ActivityItemId = "act-1",
            Reason = DialerSuppressionReason.OutsideCallingWindow,
            Description = "The destination's calling window is closed.",
            Destination = "+15550000001",
        });

        // Act
        var row = DialerDecisionLogReportProvider.Describe(interactionEvent);

        // Assert
        Assert.True(row.Suppressed);
        Assert.Equal(nameof(DialerSuppressionReason.OutsideCallingWindow), row.Reason);
        Assert.Equal("+15550000001", row.Destination);
        Assert.Equal("The destination's calling window is closed.", row.Details);
        Assert.Equal(1, row.Occurrences);
    }

    private static DialerDecisionIndex Entry(string eventType, string reason, int occurrences, DateTime lastSeenUtc)
        => new()
        {
            EventType = eventType,
            Reason = reason,
            OccurrenceCount = occurrences,
            OccurredUtc = _start,
            LastSeenUtc = lastSeenUtc,
        };
}
