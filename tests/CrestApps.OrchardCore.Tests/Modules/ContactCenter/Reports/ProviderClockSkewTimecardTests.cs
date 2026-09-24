using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The provider's clock and this one disagree, by about 0.45 s ahead live. A call's end is dated by the provider's
/// clock; the states around it are dated by this one, except the ones the end itself caused, which carry its instant.
/// These pin that such a day still adds up to the second and that no state appears to start before what caused it.
/// </summary>
public sealed class ProviderClockSkewTimecardTests
{
    private static readonly DateTime _signInUtc = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _providerAhead = TimeSpan.FromMilliseconds(445);

    [Fact]
    public async Task ADayWithAProviderDatedCallEnd_ReconcilesExactly_AndNoStateStartsBeforeItsCause()
    {
        // Arrange
        var clock = new AdvanceableClock(_signInUtc);
        var log = new AuditedEventLog(clock);
        var transitions = AgentStateAuditTestDoubles.CreateTransitions(log.Recorder, clock);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Offline };

        // Act
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Available, new AgentStateChangeContext { Source = AgentStateChangeSources.SignIn, Actor = ContactCenterActor.Agent("u1") }, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(43.3));
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Reserved, new AgentStateChangeContext { Source = AgentStateChangeSources.Reserved }, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(7.45));
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Busy, new AgentStateChangeContext { Source = AgentStateChangeSources.Accepted, Actor = ContactCenterActor.Agent("u1") }, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(3.45));

        // The caller hangs up. The provider dates it 0.445 s after this clock's now, and wrap-up is its consequence.
        var providerEndedUtc = clock.UtcNow + _providerAhead;
        await transitions.TransitionAsync(agent, AgentPresenceStatus.WrapUp, new AgentStateChangeContext { Source = AgentStateChangeSources.WrapUpStarted, ChangedUtc = providerEndedUtc }, TestContext.Current.CancellationToken);

        // A direct call's work completes at once, dated by this clock, which is still before the provider's end.
        clock.Advance(TimeSpan.FromMilliseconds(3));
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Available, new AgentStateChangeContext { Source = AgentStateChangeSources.WorkCompleted }, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(2));
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Offline, new AgentStateChangeContext { Source = AgentStateChangeSources.SignOut, Actor = ContactCenterActor.Agent("u1") }, TestContext.Current.CancellationToken);

        var timelines = AgentStateTimeline.Build(log.Events);
        var day = Assert.Single(ReconciledTimecard.Build(timelines, _signInUtc.Date, _signInUtc.Date.AddDays(1)));

        // Assert
        Assert.True(day.IsReconciled, $"Difference {day.DifferenceSeconds}s, {day.MissingTransitions} missing transition(s).");
        Assert.Equal(day.SignedInSeconds, day.StateSeconds, 9);

        var changes = log.Events
            .Where(e => e.EventType == ContactCenterConstants.Events.AgentStateChanged)
            .Select(e => e.GetData<AgentStateChangedEventData>())
            .ToArray();

        // Wrap-up starts exactly when the call ended by the provider's clock...
        var wrapUp = Assert.Single(changes, change => change.CurrentState == AgentPresenceStatus.WrapUp);
        Assert.Equal(providerEndedUtc, wrapUp.ChangedUtc);

        // ...and nothing after it starts before it, even though this clock read an earlier time when it happened.
        var completed = Assert.Single(changes, change => change.Source == AgentStateChangeSources.WorkCompleted);
        Assert.True(completed.ChangedUtc >= wrapUp.ChangedUtc);

        for (var index = 1; index < changes.Length; index++)
        {
            Assert.True(changes[index].ChangedUtc >= changes[index - 1].ChangedUtc, $"{changes[index].Source} starts before {changes[index - 1].Source}.");
        }

        // Each record is dated by the change itself, and stamped with this clock when it was written.
        foreach (var recorded in log.Events)
        {
            Assert.Equal(recorded.GetData<AgentStateChangedEventData>().ChangedUtc, recorded.OccurredUtc);
        }
    }

    [Fact]
    public void TwoChangesAtTheProvidersInstant_AreOrderedByWhenTheyWereWritten()
    {
        // Arrange
        // Wrap-up is dated by the provider's hangup, 0.445 s after this clock read when it was written. The work then
        // completes 3 ms later by this clock, which is still before the hangup's instant, so it takes that instant
        // too. Only the order they were written in says which came first, and raising each one's written time to
        // its instant made them tie -- so whichever event id sorted first won, and an agent could be left in
        // wrap-up for the rest of the day.
        var writtenUtc = _signInUtc.AddMinutes(47);
        var providerEndedUtc = writtenUtc + _providerAhead;
        var signIn = AuditEvents.State("a1", _signInUtc, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn);
        var busy = AuditEvents.State("a1", _signInUtc.AddMinutes(43), AgentPresenceStatus.Available, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted);
        var wrapUp = AuditEvents.State("a1", providerEndedUtc, AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted, recordedUtc: writtenUtc, actorType: ContactCenterActorType.System);
        var completed = AuditEvents.State("a1", providerEndedUtc, AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted, recordedUtc: writtenUtc.AddMilliseconds(3), actorType: ContactCenterActorType.System);
        var signOut = AuditEvents.State("a1", _signInUtc.AddHours(3), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut);

        // The later change sorts first by id, which is what an id tie-break would pick.
        wrapUp.ItemId = "event-z";
        completed.ItemId = "event-a";

        // Act
        var timelines = AgentStateTimeline.Build([signIn, busy, wrapUp, completed, signOut]);
        var day = Assert.Single(ReconciledTimecard.Build(timelines, _signInUtc.Date, _signInUtc.Date.AddDays(1)));
        var intervals = AgentStateTimeline.BuildIntervals(timelines, _signInUtc.Date, _signInUtc.Date.AddDays(1));

        // Assert
        Assert.True(day.IsReconciled, $"Difference {day.DifferenceSeconds}s, {day.MissingTransitions} missing transition(s).");
        Assert.Equal(0, day.States.WrapUpSeconds, 9);
        // Back to Available from the hangup's instant until the sign-off, not in wrap-up.
        Assert.Contains(intervals, interval =>
            interval.Status == AgentPresenceStatus.Available &&
            interval.StartUtc == providerEndedUtc &&
            interval.EndUtc == _signInUtc.AddHours(3));
    }
}
