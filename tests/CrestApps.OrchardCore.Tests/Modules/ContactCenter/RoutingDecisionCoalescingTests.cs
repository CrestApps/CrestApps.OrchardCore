using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A failed routing decision that repeats the queue's last one is counted on the event that recorded it, and nothing a
/// reader could tell apart is ever merged.
/// </summary>
public sealed class RoutingDecisionCoalescingTests
{
    private static readonly DateTime _first = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsRepeat_WhenTheSameFailureIsMadeAMinuteLater_IsTrue()
    {
        // Arrange
        var (previousEvent, previous) = Previous(Data());

        // Act
        var repeat = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, Data(), _first.AddMinutes(1));

        // Assert
        Assert.True(repeat);
    }

    [Fact]
    public void IsRepeat_IgnoresTheOrderOfTheCandidatesAndTheirScores()
    {
        // Arrange
        var (previousEvent, previous) = Previous(Data(
            Candidate("agent-1", eligible: false, score: 1, "Busy."),
            Candidate("agent-2", eligible: false, score: 2, "At capacity (1/1 active interactions).")));

        var next = Data(
            Candidate("agent-2", eligible: false, score: 7, "At capacity (1/1 active interactions)."),
            Candidate("agent-1", eligible: false, score: 9, "Busy."));

        // Act
        var repeat = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, next, _first.AddMinutes(1));

        // Assert
        Assert.True(repeat);
    }

    [Fact]
    public void IsRepeat_WhenACandidatesReasonChanges_IsFalse()
    {
        // Arrange
        var (previousEvent, previous) = Previous(Data(Candidate("agent-1", eligible: false, score: 0, "Busy.")));
        var next = Data(Candidate("agent-1", eligible: false, score: 0, "On break."));

        // Act
        var repeat = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, next, _first.AddMinutes(1));

        // Assert
        Assert.False(repeat);
    }

    [Fact]
    public void IsRepeat_WhenTheDecisionIsAboutAnotherRecord_IsFalse()
    {
        // Arrange
        var (previousEvent, previous) = Previous(Data());
        var next = Data();
        next.QueueItemId = "item-2";

        // Act
        var repeat = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, next, _first.AddMinutes(1));

        // Assert
        Assert.False(repeat);
    }

    [Fact]
    public void IsRepeat_NeverMergesASuccess()
    {
        // Arrange
        var (previousEvent, previous) = Previous(Data());
        var success = Data();
        success.Succeeded = true;

        var (successEvent, successData) = Previous(success);

        // Act
        var afterFailure = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, success, _first.AddMinutes(1));
        var afterSuccess = RoutingDecisionCoalescing.IsRepeat(successEvent, successData, Data(), _first.AddMinutes(1));

        // Assert
        Assert.False(afterFailure);
        Assert.False(afterSuccess);
    }

    [Fact]
    public void IsRepeat_AfterALongerPauseThanTheGap_IsFalse()
    {
        // Arrange
        var previousData = Data();
        previousData.LastSeenUtc = _first.AddMinutes(5);
        previousData.OccurrenceCount = 6;

        var (previousEvent, previous) = Previous(previousData);

        // Act
        var withinGap = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, Data(), _first.AddMinutes(5) + RoutingDecisionCoalescing.MaximumGap);
        var pastGap = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, Data(), _first.AddMinutes(5) + RoutingDecisionCoalescing.MaximumGap + TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(withinGap);
        Assert.False(pastGap);
    }

    // A run is cut after the span, so an hourly count stays close and retention, which removes events by their first
    // time, never removes a run that is still being repeated.
    [Fact]
    public void IsRepeat_OnceTheEventCoversTheMaximumSpan_IsFalse()
    {
        // Arrange
        var previousData = Data();
        previousData.LastSeenUtc = _first + RoutingDecisionCoalescing.MaximumSpan - TimeSpan.FromMinutes(1);

        var (previousEvent, previous) = Previous(previousData);

        // Act
        var repeat = RoutingDecisionCoalescing.IsRepeat(previousEvent, previous, Data(), _first + RoutingDecisionCoalescing.MaximumSpan);

        // Assert
        Assert.False(repeat);
    }

    [Fact]
    public void Occurrences_OfAnEventWrittenBeforeCoalescing_ReadAsOneDecisionMadeWhenItOccurred()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { OccurredUtc = _first };
        var legacy = new ActivityRoutingDecisionEventData { OccurrenceCount = 0 };

        // Act
        var count = RoutingDecisionCoalescing.GetOccurrenceCount(legacy);
        var lastSeen = RoutingDecisionCoalescing.GetLastSeenUtc(interactionEvent, legacy);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(_first, lastSeen);
    }

    [Fact]
    public void Payload_WithoutTheCountOrTheLastSeenTime_ReadsAsOneDecision()
    {
        // Arrange
        var interactionEvent = new InteractionEvent
        {
            OccurredUtc = _first,
            Data = """{"QueueId":"queue-1","Succeeded":false,"Reason":"No agents are currently available for this queue.","Candidates":[]}""",
        };

        // Act
        var data = interactionEvent.GetData<ActivityRoutingDecisionEventData>();

        // Assert
        Assert.Equal(1, data.OccurrenceCount);
        Assert.Null(data.LastSeenUtc);
        Assert.Equal(_first, RoutingDecisionCoalescing.GetLastSeenUtc(interactionEvent, data));
    }

    private static (InteractionEvent Event, ActivityRoutingDecisionEventData Data) Previous(ActivityRoutingDecisionEventData data)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            OccurredUtc = _first,
        };

        interactionEvent.SetData(data);

        return (interactionEvent, interactionEvent.GetData<ActivityRoutingDecisionEventData>());
    }

    private static ActivityRoutingDecisionEventData Data(params ActivityRoutingCandidateDecisionData[] candidates)
        => new()
        {
            QueueId = "queue-1",
            QueueItemId = "item-1",
            ActivityItemId = "act-1",
            Succeeded = false,
            Reason = candidates.Length == 0
                ? "No agents are currently available for this queue."
                : "No available agent matched the queue routing policy.",
            Candidates = [.. candidates],
        };

    private static ActivityRoutingCandidateDecisionData Candidate(string agentId, bool eligible, double score, params string[] reasons)
        => new()
        {
            AgentId = agentId,
            IsEligible = eligible,
            Score = score,
            Reasons = [.. reasons],
        };
}
