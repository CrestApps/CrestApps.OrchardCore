using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Live, an agent was left Busy with nothing on the line once the call they had accepted was over, and a supervisor's
/// Set Available only queued behind work that would never end. An outbound call nobody answers leaves an agent in exactly
/// that state, so these drive it through the real pacing, reservation, presence and provider-event pipeline on SQLite,
/// then run the real recovery over the state history the pipeline recorded: the Busy state change it reads comes from
/// the accept at dial time, not from the test.
/// </summary>
public sealed class StuckBusyRecoveryIntegrationTests
{
    private const string AgentId = "agent-1";
    private const string ActivityId = "activity-1";

    [Fact]
    public async Task UnansweredCall_LeavesTheAgentBusy_UntilRecoveryReturnsThemAfterTheGracePeriod()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(durableEventHistory: true);
        await harness.SignInAgentAsync(AgentId, "user-1");
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");
        var recovery = new RecoveryProbe(harness);

        // The reservation is accepted when the dial is placed, so the agent is Busy before the customer answers.
        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentId));

        var intoBusy = await recovery.LatestStateChangeAsync();
        Assert.Equal(AgentPresenceStatus.Busy, intoBusy.CurrentState);
        Assert.Equal(AgentStateChangeSources.Accepted, intoBusy.Source);

        // The customer never picks up. The call is over, but only a handled call's end releases the agent.
        harness.Clock.Advance(TimeSpan.FromSeconds(25));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.NoAnswer, "no-answer");

        var interaction = await harness.FindInteractionByActivityAsync(ActivityId);
        Assert.True(interaction.IsSettled);
        Assert.Equal(harness.Clock.UtcNow, interaction.EndedUtc);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentId));

        // Act
        // Within the grace period the call's own release could still arrive, so recovery leaves the agent alone.
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        var withinGrace = await recovery.RunAsync();
        var presenceWithinGrace = await harness.GetPresenceAsync(AgentId);

        harness.Clock.Advance(TimeSpan.FromSeconds(31));
        var afterGrace = await recovery.RunAsync();

        // Assert
        Assert.Equal(0, withinGrace);
        Assert.Equal(AgentPresenceStatus.Busy, presenceWithinGrace);

        Assert.Equal(1, afterGrace);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentId));

        // The audit tells the platform putting the agent right apart from the agent finishing their work, and names the
        // call that had made them Busy.
        var reconciled = await recovery.LatestStateChangeAsync();
        Assert.Equal(AgentPresenceStatus.Busy, reconciled.PreviousState);
        Assert.Equal(AgentPresenceStatus.Available, reconciled.CurrentState);
        Assert.Equal(AgentStateChangeSources.Reconciled, reconciled.Source);
        Assert.Equal(interaction.ItemId, reconciled.InteractionId);
        Assert.Contains(recovery.Logger.At(LogLevel.Information), message => message.StartsWith("Returned Contact Center agent 'agent-1' to work", StringComparison.Ordinal));

        // Back in a ready state, the agent is not recovered a second time.
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(0, await recovery.RunAsync());
    }

    [Fact]
    public async Task AnsweredCallStillConnected_KeepsTheAgentBusy_HoweverLongItRuns()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(durableEventHistory: true);
        await harness.SignInAgentAsync(AgentId, "user-1");
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");
        var recovery = new RecoveryProbe(harness);

        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Progressive));
        harness.Clock.Advance(TimeSpan.FromSeconds(10));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Connected, "connected");

        // Act
        // A long call is a call, not a stuck agent: nothing about the agent is touched while it is still up.
        harness.Clock.Advance(TimeSpan.FromHours(3));
        var recovered = await recovery.RunAsync();

        // Assert
        Assert.Equal(0, recovered);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentId));
        Assert.Equal(AgentStateChangeSources.Accepted, (await recovery.LatestStateChangeAsync()).Source);
    }

    // The presence manager will not move an agent who holds a reservation. That used to be counted and logged as a
    // recovery all the same, so an agent still stuck Busy read as put right.
    [Fact]
    public async Task AgentHoldingAReservation_IsNotCountedAsRecovered_AndTheLogSaysWhy()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(durableEventHistory: true);
        await harness.SignInAgentAsync(AgentId, "user-1");
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");
        var recovery = new RecoveryProbe(harness);

        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        harness.Clock.Advance(TimeSpan.FromSeconds(25));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.NoAnswer, "no-answer");

        // The accept cleared the agent's reservation, and the harness cannot offer a Busy agent more work, so the
        // reservation a next offer would leave them holding is put on the profile directly.
        var agent = await harness.AgentManager.FindByIdAsync(AgentId, TestContext.Current.CancellationToken);
        agent.ActiveReservationId = "reservation-next";
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        // Act
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        var recovered = await recovery.RunAsync();

        // Assert
        Assert.Equal(0, recovered);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentId));
        Assert.DoesNotContain(recovery.Logger.At(LogLevel.Information), message => message.StartsWith("Returned Contact Center agent", StringComparison.Ordinal));
        Assert.Contains(recovery.Logger.At(LogLevel.Warning), message => message.Contains("Could not return Contact Center agent 'agent-1' to work", StringComparison.Ordinal));
        Assert.Equal(AgentStateChangeSources.Accepted, (await recovery.LatestStateChangeAsync()).Source);
    }

    // The real recovery over the harness's own store, presence manager and recorded state history.
    private sealed class RecoveryProbe
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly IInteractionEventStore _eventStore;
        private readonly AgentAvailabilityRecoveryService _service;

        public RecoveryProbe(DialerModeIntegrationHarness harness)
        {
            _harness = harness;
            _eventStore = harness.Services.GetRequiredService<IInteractionEventStore>();
            _service = new AgentAvailabilityRecoveryService(
                harness.AgentManager,
                harness.InteractionManager,
                harness.PresenceManager,
                [harness.Services.GetRequiredService<IActivityReservationManager>()],
                _eventStore,
                Options.Create(new AgentAvailabilityOptions()),
                harness.Clock,
                Logger);
        }

        public RecordingLogger<AgentAvailabilityRecoveryService> Logger { get; } = new();

        // One background pass, committed the way its shell scope would be.
        public async Task<int> RunAsync()
        {
            var recovered = await _service.RecoverAsync(TestContext.Current.CancellationToken);
            await _harness.CommitAsync();

            return recovered;
        }

        // The agent's latest state change as the durable history holds it, which is what the recovery reads.
        public async Task<AgentStateChangedEventData> LatestStateChangeAsync()
        {
            var latest = await _eventStore.GetLatestBeforeAsync(
                nameof(AgentProfile),
                [ContactCenterConstants.Events.AgentStateChanged],
                [AgentId],
                _harness.Clock.UtcNow.AddTicks(1),
                TestContext.Current.CancellationToken);

            return Assert.Single(latest).GetData<AgentStateChangedEventData>();
        }
    }
}
