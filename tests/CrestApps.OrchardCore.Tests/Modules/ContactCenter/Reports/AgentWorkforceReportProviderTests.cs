using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class AgentWorkforceReportProviderTests
{
    [Fact]
    public void BuildIntervals_WhenStateSpansReportBoundary_ShouldClipDurationToRange()
    {
        // Arrange
        var fromUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddHours(8);
        var events = new[]
        {
            CreatePresenceEvent("agent-1", fromUtc.AddHours(-1), AgentPresenceStatus.Offline, AgentPresenceStatus.Available),
            CreatePresenceEvent("agent-1", fromUtc.AddHours(3), AgentPresenceStatus.Available, AgentPresenceStatus.Break),
            CreatePresenceEvent("agent-1", fromUtc.AddHours(4), AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            CreatePresenceEvent("agent-1", toUtc.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Offline),
        };

        // Act
        var intervals = AgentWorkforceReportProvider.BuildIntervals(events, fromUtc, toUtc);

        // Assert
        Assert.Collection(
            intervals,
            interval =>
            {
                Assert.Equal(AgentPresenceStatus.Available, interval.Status);
                Assert.Equal(TimeSpan.FromHours(3).TotalSeconds, interval.DurationSeconds);
            },
            interval =>
            {
                Assert.Equal(AgentPresenceStatus.Break, interval.Status);
                Assert.Equal(TimeSpan.FromHours(1).TotalSeconds, interval.DurationSeconds);
            },
            interval =>
            {
                Assert.Equal(AgentPresenceStatus.Available, interval.Status);
                Assert.Equal(TimeSpan.FromHours(4).TotalSeconds, interval.DurationSeconds);
            });
    }

    [Fact]
    public void BuildIntervals_WhenChangedUtcIsMissing_ShouldUseEventTime()
    {
        // Arrange
        var fromUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddHours(2);
        var first = CreatePresenceEvent("agent-1", fromUtc, AgentPresenceStatus.Offline, AgentPresenceStatus.Available);
        var second = CreatePresenceEvent("agent-1", fromUtc.AddHours(1), AgentPresenceStatus.Available, AgentPresenceStatus.Break);
        var secondData = second.GetData<AgentPresenceChangedEventData>();
        secondData.ChangedUtc = default;
        second.SetData(secondData);

        // Act
        var intervals = AgentWorkforceReportProvider.BuildIntervals([first, second], fromUtc, toUtc);

        // Assert
        Assert.Equal(2, intervals.Count);
        Assert.All(intervals, interval => Assert.Equal(TimeSpan.FromHours(1).TotalSeconds, interval.DurationSeconds));
    }

    private static InteractionEvent CreatePresenceEvent(
        string agentId,
        DateTime changedUtc,
        AgentPresenceStatus previous,
        AgentPresenceStatus current)
    {
        var interactionEvent = new InteractionEvent
        {
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            OccurredUtc = changedUtc,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = previous,
            CurrentStatus = current,
            ChangedUtc = changedUtc,
        });

        return interactionEvent;
    }
}
