using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Power campaigns pick the agent who has waited longest, by <see cref="AgentProfile.IdleSinceUtc"/>. Live, some agents
/// took far more calls than others: an agent who came back from a call without going through wrap-up kept the stamp
/// from before the call, so they were still first in line. These drive a power dial through the real pacing,
/// reservation, presence and provider-event pipeline on SQLite and read the stamp it leaves.
/// </summary>
public sealed class PowerDialerIdlePlaceIntegrationTests
{
    private const string AgentId = "agent-1";
    private const string ActivityId = "activity-1";

    [Fact]
    public async Task UnansweredDial_ReturnsTheAgentWithTheirPlaceInLine()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(releasesEndedCalls: true);
        var idleSince = await SignInIdleAgentAsync(harness);
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");

        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync(AgentId));

        // Act - nobody answers, and the ended call's routing is released as the host's handler does.
        harness.Clock.Advance(TimeSpan.FromSeconds(25));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.NoAnswer, "no-answer");
        await harness.ReleaseEndedCallAsync(ActivityId);

        // Assert - the dial never reached the agent, so they are still the one who has waited longest.
        var agent = await FindAgentAsync(harness);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(idleSince, agent.IdleSinceUtc);
    }

    [Fact]
    public async Task CallDispositionedWhileStillConnected_SendsTheAgentToTheBackOfTheLine()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(releasesEndedCalls: true);
        var idleSince = await SignInIdleAgentAsync(harness);
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");

        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Connected, "connected");

        // Act - the agent saves the disposition before hanging up, so they return to Available without wrap-up, and the
        // call's own end then finds nothing to put them in wrap-up from.
        harness.Clock.Advance(TimeSpan.FromSeconds(90));
        await harness.DispositionAsync(AgentId);
        var dispositionedUtc = harness.Clock.UtcNow;

        harness.Clock.Advance(TimeSpan.FromSeconds(5));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Ended, "ended");

        // Assert - the idle clock restarts when the work ended, not at the stamp from before the call.
        var agent = await FindAgentAsync(harness);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.NotEqual(idleSince, agent.IdleSinceUtc);
        Assert.Equal(dispositionedUtc, agent.IdleSinceUtc);
    }

    [Fact]
    public async Task AnsweredCallWrappedUp_RestartsTheIdleClockWhenTheWrapUpEnds()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(releasesEndedCalls: true);
        await SignInIdleAgentAsync(harness);
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");

        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));

        // Act
        await harness.AnswerAndHangupAsync(ActivityId);
        var duringWrapUp = (await FindAgentAsync(harness)).IdleSinceUtc;

        harness.Clock.Advance(TimeSpan.FromSeconds(45));
        await harness.DispositionAsync(AgentId);

        // Assert - not idle while wrapping up, idle from the moment the wrap-up ends.
        var agent = await FindAgentAsync(harness);
        Assert.Null(duringWrapUp);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(harness.Clock.UtcNow, agent.IdleSinceUtc);
    }

    private static async Task<DateTime> SignInIdleAgentAsync(DialerModeIntegrationHarness harness)
    {
        await harness.SignInAgentAsync(AgentId, "user-1");

        var idleSince = harness.Clock.UtcNow.AddMinutes(-10);
        var agent = await FindAgentAsync(harness);
        agent.IdleSinceUtc = idleSince;
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        return idleSince;
    }

    private static async Task<AgentProfile> FindAgentAsync(DialerModeIntegrationHarness harness)
        => await harness.AgentManager.FindByIdAsync(AgentId, TestContext.Current.CancellationToken);
}
