using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Full-workflow integration tests that drive a large campaign to completion across many pacing cycles, proving
/// pacing, routing/assignment, the agent-state lifecycle, and re-availability after disposition all hold under
/// volume.
/// </summary>
public sealed class DialerVolumeWorkflowTests
{
    [Fact]
    public async Task Progressive_100Calls_10Agents_DrainsWithFairAssignmentAndReAvailability()
    {
        // Arrange: 10 signed-in agents and 100 queued campaign activities.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        const int agentCount = 10;
        const int callCount = 100;
        await harness.SignInAgentsAsync(agentCount);
        await harness.SeedQueuedActivitiesAsync(callCount);
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Progressive);

        // Act: run pacing cycles until the queue drains, completing every call each round.
        var summary = await RunToCompletionAsync(harness, profile, agentCount);

        // Assert: every call was dialed exactly once.
        Assert.Equal(callCount, harness.Router.PlacedCalls.Count);
        Assert.Equal(callCount, harness.Router.PlacedCalls.Select(call => call.ActivityId).Distinct().Count());

        // Progressive dials one call per available agent, so a full pool clears in one call per agent per round.
        Assert.Equal(callCount / agentCount, summary.Rounds);
        Assert.All(summary.RoundSizes, size => Assert.Equal(agentCount, size));

        // Work was spread evenly across all agents (least-recently-assigned routing), and none was double-booked.
        Assert.Equal(agentCount, summary.CallsPerAgent.Count);
        Assert.All(summary.CallsPerAgent.Values, count => Assert.Equal(callCount / agentCount, count));

        // Every agent ends idle and Available for the next campaign.
        await AssertAllAvailableAsync(harness);
        Assert.Empty(await WaitingActivitiesAsync(harness));
    }

    [Fact]
    public async Task Power_100Calls_10Agents_ThrottlesPerCycleButStillDrainsAcrossAllAgents()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        const int agentCount = 10;
        const int callCount = 100;
        await harness.SignInAgentsAsync(agentCount);
        await harness.SeedQueuedActivitiesAsync(callCount);
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power, callsPerAgent: 3);

        // Act
        var summary = await RunToCompletionAsync(harness, profile, agentCount);

        // Assert: every call dialed exactly once.
        Assert.Equal(callCount, harness.Router.PlacedCalls.Count);
        Assert.Equal(callCount, harness.Router.PlacedCalls.Select(call => call.ActivityId).Distinct().Count());

        // Power paces: no cycle exceeds the hard per-cycle cap, and each cycle still assigns distinct agents.
        Assert.All(summary.RoundSizes, size => Assert.True(size <= PowerDialerStrategy.MaxCallsPerAgent, $"A Power cycle placed {size} calls, over the cap."));

        // Over many throttled cycles the work still reaches every agent, and everyone ends Available.
        Assert.Equal(agentCount, summary.CallsPerAgent.Count);
        Assert.Equal(callCount, summary.CallsPerAgent.Values.Sum());
        await AssertAllAvailableAsync(harness);
        Assert.Empty(await WaitingActivitiesAsync(harness));
    }

    /// <summary>
    /// Runs pacing cycles until nothing more can be dialed, completing every call placed in a round (answer →
    /// hang up → wrap-up → disposition → available) before the next cycle, and asserting the agent-state
    /// invariants along the way.
    /// </summary>
    private static async Task<WorkflowSummary> RunToCompletionAsync(
        DialerModeIntegrationHarness harness,
        DialerProfile profile,
        int agentCount)
    {
        var callsPerAgent = new Dictionary<string, int>(StringComparer.Ordinal);
        var roundSizes = new List<int>();
        var processed = 0;
        var rounds = 0;

        while (true)
        {
            var started = await harness.RunPacingCycleAsync(profile);

            if (started == 0)
            {
                break;
            }

            rounds++;
            Assert.True(rounds <= 1000, "Pacing did not converge; aborting to avoid an infinite loop.");

            var roundCalls = harness.Router.PlacedCalls.Skip(processed).ToList();
            processed += roundCalls.Count;

            Assert.Equal(started, roundCalls.Count);
            Assert.True(roundCalls.Count <= agentCount, "A cycle placed more calls than there are agents.");

            // No agent is assigned two simultaneous live calls, and each dialing agent is Busy.
            Assert.Equal(roundCalls.Count, roundCalls.Select(call => call.AgentId).Distinct().Count());

            foreach (var call in roundCalls)
            {
                Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(call.AgentId));
            }

            roundSizes.Add(roundCalls.Count);

            // Complete each call and disposition it, returning the agent to Available for the next cycle.
            foreach (var call in roundCalls)
            {
                await harness.AnswerAndHangupAsync(call.ActivityId);
                Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(call.AgentId));

                await harness.DispositionAsync(call.AgentId);
                Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(call.AgentId));

                callsPerAgent[call.AgentId] = callsPerAgent.GetValueOrDefault(call.AgentId) + 1;
            }
        }

        return new WorkflowSummary(rounds, roundSizes, callsPerAgent);
    }

    private static async Task AssertAllAvailableAsync(DialerModeIntegrationHarness harness)
    {
        foreach (var agentId in harness.AgentIds)
        {
            Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(agentId));
        }
    }

    private static async Task<IReadOnlyCollection<QueueItem>> WaitingActivitiesAsync(DialerModeIntegrationHarness harness)
        => await harness.GetWaitingQueueItemsAsync();

    private sealed record WorkflowSummary(int Rounds, List<int> RoundSizes, Dictionary<string, int> CallsPerAgent);
}
