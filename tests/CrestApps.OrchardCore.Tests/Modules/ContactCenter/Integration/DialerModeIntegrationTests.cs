using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// End-to-end integration tests that drive each outbound dialing mode through the real pacing, reservation,
/// presence, and provider-event pipeline and assert the agent-state lifecycle at every step.
/// </summary>
public sealed class DialerModeIntegrationTests
{
    [Fact]
    public async Task Power_AnsweredCall_MovesAgentThroughReservedBusyWrapUpAndBackToAvailable()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power, callsPerAgent: 3);

        // Act + Assert: a pacing cycle reserves the agent and places the call. The agent is held Reserved while the
        // customer is reached, not on a call.
        var started = await harness.RunPacingCycleAsync(profile);

        Assert.Equal(1, started);
        Assert.Single(harness.Router.PlacedCalls);
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-1"));

        // The customer answers and the agent is connected: now they are on a call.
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.Connected, "connected");
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-1"));

        // The call ends: a campaign call moves the agent into wrap-up.
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.Ended, "ended");
        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync("agent-1"));

        // Dispositioning the call returns the agent to Available for the next assignment.
        await harness.DispositionAsync("agent-1");
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync("agent-1"));
    }

    [Fact]
    public async Task Power_WithMultipleAgents_PlacesOneCallPerAvailableAgentUpToCap()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SignInAgentAsync("agent-2", "user-2");
        await harness.SignInAgentAsync("agent-3", "user-3");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        await harness.SeedQueuedActivityAsync("activity-2", "+15551230002");
        await harness.SeedQueuedActivityAsync("activity-3", "+15551230003");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power, callsPerAgent: 3);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert
        Assert.Equal(3, started);
        Assert.Equal(3, harness.Router.PlacedCalls.Count);
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-1"));
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-2"));
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-3"));
    }

    [Fact]
    public async Task Progressive_DialsOnePerAvailableAgent_AndEndingOneCallOnlyAffectsThatAgent()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SignInAgentAsync("agent-2", "user-2");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        await harness.SeedQueuedActivityAsync("activity-2", "+15551230002");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Progressive);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert: exactly one call per available agent, both held Reserved while their customers are dialed.
        Assert.Equal(2, started);
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-1"));
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync("agent-2"));

        // Ending one agent's call moves only that agent to wrap-up; the other is still held for theirs.
        var firstCall = harness.Router.PlacedCalls[0];
        var firstAgentId = firstCall.AgentId;
        var otherAgentId = firstAgentId == "agent-1" ? "agent-2" : "agent-1";

        await harness.AnswerAndHangupAsync(firstCall.ActivityId);

        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(firstAgentId));
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync(otherAgentId));
    }

    // A power dial that nobody answers was never the agent's call: they go straight back to Available, and no call is on
    // their record.
    [Fact]
    public async Task Power_UnansweredCall_NeverPutsTheAgentOnACall()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(releasesEndedCalls: true);
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power, callsPerAgent: 3);
        await harness.RunPacingCycleAsync(profile);

        // Act
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.NoAnswer, "no-answer");
        await harness.ReleaseEndedCallAsync("activity-1");

        // Assert
        var agent = await harness.AgentManager.FindByIdAsync("agent-1", TestContext.Current.CancellationToken);
        Assert.Equal(AgentPresenceStatus.Available, agent!.PresenceStatus);
        Assert.Null(agent.DialHoldReservationId);
    }

    [Theory]
    [InlineData(DialerMode.Manual)]
    [InlineData(DialerMode.Preview)]
    public async Task AgentDrivenModes_RunNoAutomatedCycle(DialerMode mode)
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(mode);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert: Manual and Preview are agent-initiated; the pacing engine never places a call for them.
        Assert.Equal(0, started);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync("agent-1"));
    }
}
