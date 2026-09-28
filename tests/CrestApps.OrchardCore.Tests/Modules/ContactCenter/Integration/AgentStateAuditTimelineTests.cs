using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A payroll timecard is built from an agent's recorded transitions, so a whole working day driven through the
/// real presence, reservation and provider-event pipeline has to come back as one unbroken chain of states whose
/// durations add up to exactly the time between signing in and signing out.
/// </summary>
public sealed class AgentStateAuditTimelineTests
{
    [Fact]
    public async Task AWorkingDay_RecordsContiguousTransitions_WhoseDurationsSumToTheSignedInTime()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Start the day signed out, so the day opens with a real sign-in.
        await harness.PresenceManager.SignOutAsync("user-1", cancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var eventsBeforeTheDay = harness.PublishedEvents.Count;

        // Act
        var signedInUtc = harness.Clock.UtcNow;
        await harness.PresenceManager.SignInAsync(
            "user-1",
            [DialerModeIntegrationHarness.QueueId],
            [DialerModeIntegrationHarness.CampaignId],
            cancellationToken);
        harness.Clock.Advance(TimeSpan.FromSeconds(12.5));

        // Reserved, then Busy as the dial is placed.
        await harness.RunPacingCycleAsync(profile);
        harness.Clock.Advance(TimeSpan.FromSeconds(4));

        // Answered, talked for 30 seconds, hung up: wrap-up.
        await harness.AnswerAndHangupAsync("activity-1");
        harness.Clock.Advance(TimeSpan.FromSeconds(41.75));

        // Dispositioned: back to Available.
        await harness.DispositionAsync("agent-1");
        harness.Clock.Advance(TimeSpan.FromMinutes(7));

        await harness.PresenceManager.SetPresenceAsync("user-1", AgentPresenceStatus.Break, "Lunch", cancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(30));

        var signedOutUtc = harness.Clock.UtcNow;
        await harness.PresenceManager.SignOutAsync("user-1", cancellationToken);

        // Assert
        var transitions = harness.PublishedEvents
            .Skip(eventsBeforeTheDay)
            .Where(e => e.EventType == ContactCenterConstants.Events.AgentStateChanged)
            .Select(e => e.GetData<AgentStateChangedEventData>())
            .Where(change => change.AgentId == "agent-1")
            .ToList();

        Assert.Equal(
            [
                AgentPresenceStatus.Available,
                AgentPresenceStatus.Reserved,
                AgentPresenceStatus.Busy,
                AgentPresenceStatus.WrapUp,
                AgentPresenceStatus.Available,
                AgentPresenceStatus.Break,
                AgentPresenceStatus.Offline,
            ],
            transitions.Select(change => change.CurrentState));
        Assert.Equal(AgentStateChangeSources.SignIn, transitions[0].Source);
        Assert.Equal(AgentStateChangeSources.SignOut, transitions[^1].Source);

        // Each transition leaves the state the previous one entered, and never goes back in time.
        for (var index = 1; index < transitions.Count; index++)
        {
            Assert.Equal(transitions[index - 1].CurrentState, transitions[index].PreviousState);
            Assert.True(transitions[index].ChangedUtc >= transitions[index - 1].ChangedUtc);
        }

        var signedInTime = transitions[^1].ChangedUtc - transitions[0].ChangedUtc;
        var accountedFor = TimeSpan.Zero;

        for (var index = 0; index < transitions.Count - 1; index++)
        {
            accountedFor += transitions[index + 1].ChangedUtc - transitions[index].ChangedUtc;
        }

        Assert.Equal(signedInTime, accountedFor);

        // The day starts when the agent signed in and ends when they signed out, to the tick.
        Assert.Equal(signedInUtc, transitions[0].ChangedUtc);
        Assert.Equal(signedOutUtc, transitions[^1].ChangedUtc);
        Assert.Equal(signedOutUtc - signedInUtc, signedInTime);
    }
}
