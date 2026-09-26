using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class AgentStateTimelineTests
{
    private static readonly DateTime _from = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = _from.AddHours(8);

    [Fact]
    public void BuildIntervals_FromAuditedCall_CountsReservedBusyAndWrapUpAsThemselves()
    {
        // Arrange
        var signIn = _from.AddHours(1);
        var events = new[]
        {
            State("agent-1", signIn, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", signIn.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, actorType: ContactCenterActorType.System),
            State("agent-1", signIn.AddHours(1).AddSeconds(5), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            State("agent-1", signIn.AddHours(1).AddMinutes(5), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted, actorType: ContactCenterActorType.System),
            State("agent-1", signIn.AddHours(1).AddMinutes(6), AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
            State("agent-1", signIn.AddHours(3), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };

        // Act
        var intervals = AgentStateTimeline.BuildIntervals(events, _from, _to);
        var summary = AgentTimeSummary.Create(intervals);

        // Assert
        Assert.Equal(5, summary.ReservedSeconds);
        Assert.Equal(295, summary.BusySeconds);
        Assert.Equal(60, summary.WrapUpSeconds);
        Assert.Equal(TimeSpan.FromHours(3).TotalSeconds, summary.SignedInSeconds);
        Assert.Equal(TimeSpan.FromHours(3).TotalSeconds - 360, summary.AvailableSeconds);
        Assert.All(intervals, interval => Assert.True(interval.FromAudit));
    }

    [Fact]
    public void Build_WhenTheAuditStartsMidPeriod_ReadsTheOlderEventsOnlyBeforeIt()
    {
        // Arrange
        var auditStart = _from.AddHours(2);
        var events = new[]
        {
            Presence("agent-1", _from, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, CrestApps.OrchardCore.ContactCenter.ContactCenterConstants.Events.AgentSignedIn),
            Presence("agent-1", _from.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Break),

            // From here both are written for each change; only the audit may count.
            Presence("agent-1", auditStart, AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            State("agent-1", auditStart, AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            State("agent-1", auditStart.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            State("agent-1", auditStart.AddHours(1).AddMinutes(1), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            Presence("agent-1", auditStart.AddHours(2), AgentPresenceStatus.Busy, AgentPresenceStatus.Available),
            State("agent-1", auditStart.AddHours(2), AgentPresenceStatus.Busy, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
        };

        // Act
        var timeline = Assert.Single(AgentStateTimeline.Build(events));
        var intervals = AgentStateTimeline.BuildIntervals([timeline], _from, _to);
        var summary = AgentTimeSummary.Create(intervals);

        // Assert
        Assert.Equal(auditStart, timeline.AuditSinceUtc);
        Assert.Equal(6, timeline.Effective.Count);
        Assert.Equal((_to - _from).TotalSeconds, summary.SignedInSeconds);
        Assert.Equal(TimeSpan.FromHours(1).TotalSeconds, summary.BreakSeconds);
        Assert.Equal(60, summary.ReservedSeconds);
        Assert.Equal(TimeSpan.FromMinutes(59).TotalSeconds, summary.BusySeconds);
        Assert.False(intervals[0].FromAudit);
        Assert.True(intervals[^1].FromAudit);
    }

    [Fact]
    public void BuildIntervals_StartsThePeriodInTheStateTheAgentWasAlreadyIn()
    {
        // Arrange: the agent took a call before the period and was still on it when it opened.
        var events = new[]
        {
            State("agent-1", _from.AddMinutes(-20), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            State("agent-1", _from.AddMinutes(10), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted),
        };

        // Act
        var intervals = AgentStateTimeline.BuildIntervals(events, _from, _from.AddHours(1));

        // Assert
        Assert.Collection(
            intervals,
            interval =>
            {
                Assert.Equal(AgentPresenceStatus.Busy, interval.Status);
                Assert.Equal(_from, interval.StartUtc);
                Assert.Equal(600, interval.DurationSeconds);
            },
            interval =>
            {
                Assert.Equal(AgentPresenceStatus.WrapUp, interval.Status);
                Assert.Equal(3000, interval.DurationSeconds);
            });
    }

    [Fact]
    public void Build_SignOffDatedByTheLastHeartbeat_SupersedesWhatWasRecordedAfterIt()
    {
        // Arrange: the agent's last heartbeat was 10:00; routing released a reservation at 10:01; the sweep noticed at
        // 10:02 and signed the agent off as of 10:00.
        var heartbeat = _from.AddHours(2);
        var events = new[]
        {
            State("agent-1", _from, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", heartbeat.AddMinutes(-1), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            State("agent-1", heartbeat.AddMinutes(1), AgentPresenceStatus.Reserved, AgentPresenceStatus.Available, AgentStateChangeSources.Released, actorType: ContactCenterActorType.System),
            State("agent-1", heartbeat, AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SessionExpired, recordedUtc: heartbeat.AddMinutes(2), actorType: ContactCenterActorType.System),
        };

        // Act
        var timeline = Assert.Single(AgentStateTimeline.Build(events));
        var spans = timeline.BuildSignedInSpans(_from, _to);
        var intervals = AgentStateTimeline.BuildIntervals([timeline], _from, _to);

        // Assert
        var released = Assert.Single(timeline.Transitions, transition => transition.Source == AgentStateChangeSources.Released);
        Assert.True(released.Superseded);
        Assert.DoesNotContain(timeline.Transitions, transition => transition.BreaksChain);

        var span = Assert.Single(spans);
        Assert.Equal(heartbeat, span.EndUtc);
        Assert.True(span.EndedBySignOff);
        Assert.Equal(span.DurationSeconds, AgentTimeSummary.Create(intervals).SignedInSeconds);
    }

    [Fact]
    public void Build_WhenATransitionIsMissing_MarksTheChangeAfterTheGap()
    {
        // Arrange: nothing recorded the move to Busy.
        var events = new[]
        {
            State("agent-1", _from, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _from.AddHours(1), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted),
        };

        // Act
        var timeline = Assert.Single(AgentStateTimeline.Build(events));

        // Assert
        Assert.False(timeline.Transitions[0].BreaksChain);
        Assert.True(timeline.Transitions[1].BreaksChain);
        Assert.Equal(AgentPresenceStatus.Available, timeline.Transitions[1].ExpectedPreviousState);
    }

    [Fact]
    public void BuildSignedInSpans_AgentStillSignedInAtTheEnd_IsClippedToIt()
    {
        // Arrange
        var events = new[]
        {
            State("agent-1", _from.AddHours(-3), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _from.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Break),
        };

        // Act
        var timeline = Assert.Single(AgentStateTimeline.Build(events));
        var span = Assert.Single(timeline.BuildSignedInSpans(_from, _to));

        // Assert
        Assert.Equal(_from, span.StartUtc);
        Assert.Equal(_to, span.EndUtc);
        Assert.False(span.EndedBySignOff);
    }

    [Fact]
    public void Build_KeepsWhoMadeEachChange()
    {
        // Arrange
        var events = new[]
        {
            State("agent-1", _from, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _from.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Break, AgentStateChangeSources.SetState, actorType: ContactCenterActorType.Supervisor, reason: "Coaching"),
        };

        // Act
        var timeline = Assert.Single(AgentStateTimeline.Build(events));

        // Assert
        Assert.Equal(ContactCenterActorType.Agent, timeline.Transitions[0].ActorType);
        Assert.Equal(ContactCenterActorType.Supervisor, timeline.Transitions[1].ActorType);
        Assert.Equal("Coaching", timeline.Transitions[1].Reason);
    }
}
