using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class AgentActivityTimelineTests
{
    private static readonly DateTime _from = new(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = _from.AddHours(2);

    [Fact]
    public void Build_ListsStatesOffersAndCallChangesInOrder_WithDurationsAndWhoMadeThem()
    {
        // Arrange
        var reserved = _from.AddMinutes(10);
        var timelines = AgentStateTimeline.Build(
        [
            State("agent-1", _from.AddMinutes(-30), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", reserved, AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, actorType: ContactCenterActorType.System, interactionId: "call-1"),
            State("agent-1", reserved.AddSeconds(8), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted, interactionId: "call-1"),
            State("agent-1", reserved.AddMinutes(6), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted, actorType: ContactCenterActorType.System, interactionId: "call-1"),
        ]);
        var offers = new[]
        {
            Offer(ContactCenterConstants.Events.OfferPresented, "reservation-1", "call-1", "agent-1", reserved),
            Offer(ContactCenterConstants.Events.OfferAccepted, "reservation-1", "call-1", "agent-1", reserved, reserved.AddSeconds(8)),
            Offer(ContactCenterConstants.Events.OfferAccepted, "reservation-9", "call-9", "agent-9", reserved, reserved.AddSeconds(2)),
        };
        var calls = new[]
        {
            Call(ContactCenterConstants.Events.CallHeld, "call-1", reserved.AddMinutes(2), "agent-1", actorType: ContactCenterActorType.Agent),
            Call(ContactCenterConstants.Events.CallResumed, "call-1", reserved.AddMinutes(3), "agent-1", durationSeconds: 60, actorType: ContactCenterActorType.Agent),
            Call(ContactCenterConstants.Events.CallEnded, "call-9", reserved.AddMinutes(3)),
        };

        // Act
        var entries = AgentActivityTimeline.Build(timelines, [], offers, calls, _from, _to);

        // Assert
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(AgentActivityKind.State, entry.Kind);
                Assert.Equal("Available", entry.Name);
                Assert.Equal(_from, entry.StartUtc);
                Assert.Equal(600, entry.DurationSeconds);
                Assert.Contains("in this state since", entry.Detail, StringComparison.Ordinal);
            },
            entry =>
            {
                Assert.Equal("Reserved", entry.Name);
                Assert.Equal(ContactCenterActorType.System, entry.ActorType);
                Assert.Equal(8, entry.DurationSeconds);
            },
            entry =>
            {
                Assert.Equal(AgentActivityKind.Offer, entry.Kind);
                Assert.Equal(ContactCenterConstants.Events.OfferAccepted, entry.Name);
                Assert.Equal(8, entry.DurationSeconds);
            },
            entry =>
            {
                Assert.Equal("Busy", entry.Name);
                Assert.Equal(ContactCenterActorType.Agent, entry.ActorType);
            },
            entry =>
            {
                Assert.Equal(ContactCenterConstants.Events.CallHeld, entry.Name);
                Assert.Null(entry.EndUtc);
            },
            entry =>
            {
                // The resume carries the hold it ended, so it spans it.
                Assert.Equal(ContactCenterConstants.Events.CallResumed, entry.Name);
                Assert.Equal(reserved.AddMinutes(2), entry.StartUtc);
                Assert.Equal(60, entry.DurationSeconds);
            },
            entry =>
            {
                Assert.Equal("WrapUp", entry.Name);
                Assert.Equal(_to, entry.EndUtc);
            });
    }
}
