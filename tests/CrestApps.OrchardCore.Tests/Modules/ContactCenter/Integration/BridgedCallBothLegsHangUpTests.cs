using System.Diagnostics;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Guards the half-minute wait between an agent hanging up a bridged call and the agent's wrap-up, end to end through
/// the real Contact Center services on a real SQLite store. Both legs of the call hung up at once and their webhooks
/// were processed side by side: the caller's leg held the call's ingestion lease and needed to write the ended call,
/// while the agent's leg had already written its own work (its call-quality record), so its open transaction held
/// SQLite's only write lock, and it then waited for the same call's lease to end the call from its side.
/// </summary>
public sealed class BridgedCallBothLegsHangUpTests
{
    private const string ActivityId = "activity-1";
    private const string AgentId = "agent-1";
    private const string AgentLegId = "agent-leg-1";

    [Fact]
    public async Task BothLegsHangingUpAtOnce_EndTheCallAtTheCallersHangup_AndStartTheAgentsWrapUpPromptly()
    {
        // Arrange: a Power-dialed campaign call, answered by the customer, with the agent's leg answered and joined.
        // A one-second busy timeout stands in for the live thirty seconds, so a flow blocked on the write lock fails
        // with "database is locked" quickly instead of stalling the test.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(busyTimeoutSeconds: 1);
        var cancellationToken = TestContext.Current.CancellationToken;

        await harness.SignInAgentAsync(AgentId, "user-1");
        await harness.SeedQueuedActivityAsync(ActivityId, "+15551230001");
        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Connected, "connected");

        var callId = (await harness.FindInteractionByActivityAsync(ActivityId)).ProviderInteractionId;

        Assert.True(await harness.Services.GetRequiredService<IContactCenterAgentLegFailureService>().RecordAnsweredAsync(
            DialerModeIntegrationHarness.ProviderName,
            callId,
            AgentLegId,
            cancellationToken));
        await harness.CommitAsync();
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentId));

        harness.Clock.Advance(TimeSpan.FromSeconds(84));
        var callerHungUpUtc = harness.Clock.UtcNow;
        var agentLegHungUpUtc = callerHungUpUtc.AddMilliseconds(400);

        // Each leg's webhook is its own delivery, with its own session, serialized on the call by the same lock.
        var distributedLock = new FakeDistributedLock();
        await using var callerFlow = harness.OpenFlow(distributedLock);
        await using var agentFlow = harness.OpenFlow(distributedLock);

        var callerLegHoldsTheCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agentLegHasWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        // The caller's leg hangs up: the provider-neutral fan-out takes the call's lease, and the Contact Center's
        // ingestion runs inside it (re-entrantly) to end the call and start the agent's wrap-up.
        var callerLeg = Task.Run(async () =>
        {
            var gate = callerFlow.Services.GetRequiredService<IVoiceIngressGate>();

            await using (await gate.AcquireAsync(DialerModeIntegrationHarness.ProviderName, callId, cancellationToken))
            {
                callerLegHoldsTheCall.SetResult();
                await agentLegHasWritten.Task;

                await callerFlow.Services.GetRequiredService<IProviderVoiceEventService>().IngestAsync(new ProviderVoiceEvent
                {
                    ProviderName = DialerModeIntegrationHarness.ProviderName,
                    ProviderCallId = callId,
                    State = VoiceCallState.Ended,
                    HangupCause = HangupCause.NormalClearing,
                    OccurredUtc = callerHungUpUtc,
                    IdempotencyKey = $"{callId}:hangup",
                }, cancellationToken);
            }
        }, cancellationToken);

        // The agent's leg hangs up at the same moment: its delivery has already written (and flushed) its own work,
        // then it ends the call from the agent's side, which waits on the same call's lease.
        var agentLeg = Task.Run(async () =>
        {
            await callerLegHoldsTheCall.Task;

            await agentFlow.Session.SaveAsync(new CallQualityRecord
            {
                ItemId = "agent-leg-quality",
                RecordKey = $"provider:{AgentLegId}",
                ProviderCallControlId = AgentLegId,
                ObservedUtc = agentLegHungUpUtc,
            }, cancellationToken: cancellationToken);
            await agentFlow.Session.FlushAsync(cancellationToken);

            agentLegHasWritten.SetResult();

            await agentFlow.Services.GetRequiredService<IContactCenterAgentLegFailureService>().RecordEndedAsync(
                DialerModeIntegrationHarness.ProviderName,
                callId,
                AgentLegId,
                agentLegHungUpUtc,
                HangupCause.NormalClearing,
                cancellationToken);

            await agentFlow.Session.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        // Act: neither leg may fail on the database's write lock or on the lease's wait.
        await Task.WhenAll(callerLeg, agentLeg).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        stopwatch.Stop();

        // Assert: read back through a fresh unit of work, as the agent's screen would.
        await using var reader = harness.OpenFlow(distributedLock);

        var interaction = await reader.Services.GetRequiredService<IInteractionManager>()
            .FindByActivityIdAsync(ActivityId, cancellationToken);
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(callerHungUpUtc, interaction.EndedUtc);

        var agent = await reader.Services.GetRequiredService<IAgentProfileManager>().FindByIdAsync(AgentId, cancellationToken);
        Assert.Equal(AgentPresenceStatus.WrapUp, agent.PresenceStatus);

        var wrapUp = Assert.Single(
            callerFlow.PublishedEvents.Concat(agentFlow.PublishedEvents)
                .Where(e => e.EventType == ContactCenterConstants.Events.AgentStateChanged)
                .Select(e => e.GetData<AgentStateChangedEventData>()),
            change => change.AgentId == AgentId);
        Assert.Equal(AgentPresenceStatus.Busy, wrapUp.PreviousState);
        Assert.Equal(AgentPresenceStatus.WrapUp, wrapUp.CurrentState);
        Assert.Equal(AgentStateChangeSources.WrapUpStarted, wrapUp.Source);
        Assert.Equal(callerHungUpUtc, wrapUp.ChangedUtc);

        // The agent leg's own work is kept, not lost to the lock it gave up.
        Assert.NotNull(await reader.Session.Query<CallQualityRecord>().FirstOrDefaultAsync(cancellationToken));

        // Promptly: neither the database's busy timeout nor the lease's thirty-second wait was waited out.
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }
}
