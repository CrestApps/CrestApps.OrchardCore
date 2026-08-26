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
    public async Task Power_AnsweredCall_MovesAgentThroughBusyWrapUpAndBackToAvailable()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateProfile(DialerMode.Power, callsPerAgent: 3);

        // Act + Assert: a pacing cycle reserves the agent and places the call, moving them to Busy.
        var started = await harness.RunPacingCycleAsync(profile);

        Assert.Equal(1, started);
        Assert.Single(harness.Router.PlacedCalls);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-1"));

        // The customer answers, then the call ends: a campaign call moves the agent into wrap-up.
        await harness.AnswerAndHangupAsync("activity-1");
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
        var profile = harness.CreateProfile(DialerMode.Power, callsPerAgent: 3);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert
        Assert.Equal(3, started);
        Assert.Equal(3, harness.Router.PlacedCalls.Count);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-1"));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-2"));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-3"));
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
        var profile = harness.CreateProfile(DialerMode.Progressive);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert: exactly one call per available agent, both Busy.
        Assert.Equal(2, started);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-1"));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync("agent-2"));

        // Ending one agent's call moves only that agent to wrap-up; the other keeps talking.
        var firstCall = harness.Router.PlacedCalls[0];
        var firstAgentId = firstCall.AgentId;
        var otherAgentId = firstAgentId == "agent-1" ? "agent-2" : "agent-1";

        await harness.AnswerAndHangupAsync(firstCall.ActivityId);

        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(firstAgentId));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(otherAgentId));
    }

    [Fact]
    public async Task Predictive_IsRefused_NoCallPlacedAndAgentStaysAvailable()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        await harness.SignInAgentAsync("agent-1", "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateProfile(DialerMode.Predictive);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert: the blocked mode resolves to no strategy, so nothing is dialed and the agent is untouched.
        Assert.Equal(0, started);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync("agent-1"));
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
        var profile = harness.CreateProfile(mode);

        // Act
        var started = await harness.RunPacingCycleAsync(profile);

        // Assert: Manual and Preview are agent-initiated; the pacing engine never places a call for them.
        Assert.Equal(0, started);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync("agent-1"));
    }
}
