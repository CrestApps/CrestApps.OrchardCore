using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Drives over-dialing end to end through the real pacer, attempt service, provider event service, agent connector,
/// claim, abandonment tracker and policy on a SQLite store: calls placed with no agent, an agent claimed when a person
/// answers, the abandoned-call message when nobody is free, and the races that must never claim an agent twice or place
/// more calls than one cycle calculated.
/// </summary>
public sealed class PredictiveOverDialIntegrationTests
{
    private const string AgentOne = "agent-1";
    private const string AgentTwo = "agent-2";

    [Fact]
    public async Task OverDial_PlacesMoreCallsThanFreeAgents_WithNoAgentReserved()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(10);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);

        // Act
        var started = await harness.RunPredictiveCycleAsync(profile);

        // Assert: more calls than the two free agents, none of them for an agent, and nobody reserved.
        var state = await harness.Services.GetRequiredService<IPredictivePacingStateStore>()
            .FindByQueueIdAsync(harness.PacingQueueId, TestContext.Current.CancellationToken);

        Assert.NotNull(state);
        Assert.Equal(PredictivePacingDecisionMode.OverDial, state.LastDecision.Mode);
        Assert.Equal(state.LastDecision.DialCount, started);
        Assert.True(started > 2, $"Over-dialing two agents at a 10% answer rate should place more than two calls, placed {started}.");
        Assert.Equal(started, state.LastDecision.Dialed);
        Assert.Equal(1, state.Sequence);

        Assert.Equal(started, harness.Router.PlacedCalls.Count);
        Assert.All(harness.Router.PlacedCalls, call =>
        {
            Assert.Null(call.AgentId);
            Assert.Null(call.AgentUserId);
            Assert.Equal(profile.CallerId, call.CallerId);
        });
        Assert.Empty(await harness.GetReservationsAsync());
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentTwo));
        Assert.Equal(started, await harness.CountInFlightAsync());

        var interaction = await harness.FindInteractionByActivityAsync(harness.Router.PlacedCalls[0].ActivityId);
        Assert.True(DialerCallMetadata.IsOverDialed(interaction));
        Assert.Null(interaction.AgentId);

        // A second cycle at once counts the calls already ringing and places none.
        Assert.Equal(0, await harness.RunPredictiveCycleAsync(profile));
        Assert.Equal(started, harness.Router.PlacedCalls.Count);
    }

    [Fact]
    public async Task HumanAnswer_ClaimsTheAgentIdleLongest_AcceptedAndBusy_ThenWrapsUpAfterTheCall()
    {
        // Arrange: agent-2 has waited longer than agent-1.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await SetLastAssignedAsync(harness, AgentOne, harness.Clock.UtcNow.AddMinutes(-1));
        await SetLastAssignedAsync(harness, AgentTwo, harness.Clock.UtcNow.AddMinutes(-10));
        await harness.SeedQueuedActivitiesAsync(10);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        await harness.RunPredictiveCycleAsync(profile);
        var activityId = harness.Router.PlacedCalls[0].ActivityId;
        var inFlight = await harness.CountInFlightAsync();

        // Act
        await harness.RaiseHumanAnswerAsync(activityId);

        // Assert: agent-2 is claimed through an accepted reservation, Busy, and the Answer command connects them.
        var reservation = Assert.Single(await harness.GetReservationsAsync());
        Assert.Equal(AgentTwo, reservation.AgentId);
        Assert.Equal(ReservationStatus.Accepted, reservation.Status);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentTwo));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));

        var queueItem = await harness.FindQueueItemAsync(activityId);
        Assert.Equal(QueueItemStatus.Assigned, queueItem.Status);
        Assert.Equal(AgentTwo, queueItem.AgentId);
        Assert.Equal(reservation.ItemId, queueItem.ReservationId);
        Assert.Equal(inFlight - 1, await harness.CountInFlightAsync());

        var interaction = await harness.FindInteractionByActivityAsync(activityId);
        Assert.Equal(AgentTwo, interaction.AgentId);
        Assert.True(DialerCallMetadata.IsAgentClaimed(interaction));
        Assert.False(DialerCallMetadata.IsAbandoned(interaction));

        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Equal(reservation.ItemId, answer.ReservationId);
        Assert.Contains($"\"AgentId\":\"{AgentTwo}\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains(harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.DialerAgentConnectClaimed && e.InteractionId == interaction.ItemId);
        Assert.Contains(harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.QueueItemAssigned && e.AggregateId == reservation.ItemId);
        Assert.Empty(harness.Shared.Treatment.EndedWithMessage);

        // The agent's leg joins, the call ends: the agent wraps it up, and the queue item is done.
        await harness.RaiseAgentLegAnsweredAsync(activityId);
        harness.Clock.Advance(TimeSpan.FromSeconds(40));
        await harness.RaiseCallEndedAsync(activityId);

        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(AgentTwo));
        Assert.Equal(QueueItemStatus.Completed, (await harness.FindQueueItemAsync(activityId)).Status);
    }

    [Fact]
    public async Task AnswerRace_TwoAnsweredCallsAndOneAgent_ClaimTheAgentOnce_AndAbandonTheOther()
    {
        // Two answered calls want the only free agent at once, fifty times over. The fake lock grants every key at once,
        // as process-local locks on two nodes do, and holds both connects at a barrier so they set off together: what
        // keeps them apart is the claim's re-check and compare-and-set, never the lock.
        for (var round = 0; round < 50; round++)
        {
            await RunAnswerRaceAsync(round);
        }
    }

    [Fact]
    public async Task NoAgentFree_PlaysTheAbandonedCallMessage_CountsItAbandoned_NoWrapUp_RemovesTheItem_AndRetriesWithAReservedAgent()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));

        // The agent steps away while the call rings.
        await SetPresenceAsync(harness, AgentOne, AgentPresenceStatus.Break);

        // Act
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: the person hears the message at once, and the call is counted abandoned for nobody being free.
        var message = Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(interaction.ProviderInteractionId, message.CallId);
        Assert.Equal(DialerAbandonment.Reasons.NoAgentAvailable, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Null(interaction.AgentId);
        Assert.Contains(harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.DialerCallAbandoned && e.InteractionId == interaction.ItemId);
        Assert.Empty(await harness.GetReservationsAsync());
        Assert.Contains(harness.PacingQueueId, harness.Shared.Pacing.Requests);

        // The call ends: nobody wraps it up, and its item leaves the queue rather than counting as in flight.
        harness.Clock.Advance(TimeSpan.FromSeconds(12));
        await harness.RaiseCallEndedAsync("activity-1");

        Assert.Equal(AgentPresenceStatus.Break, await harness.GetPresenceAsync(AgentOne));
        Assert.Equal(QueueItemStatus.Removed, (await harness.FindQueueItemAsync("activity-1")).Status);
        Assert.Equal(0, await harness.CountInFlightAsync());

        // The activity is queued again later: it is retried with the agent reserved for it, never without one.
        await SetPresenceAsync(harness, AgentOne, AgentPresenceStatus.Available);
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));

        var retry = harness.Router.PlacedCalls[^1];
        Assert.Equal("activity-1", retry.ActivityId);
        Assert.Equal(AgentOne, retry.AgentId);
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentOne));
        Assert.Equal(0, await harness.CountInFlightAsync());
    }

    [Fact]
    public async Task MachineAnswer_IsHungUpWithoutClaimingAnAgent_AndIsNotCountedAbandoned()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile(p => p.AnsweringMachineDetection = DialerAnsweringMachineDetection.Standard);
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));

        // Act: the call is answered, the provider is still listening, then says a machine answered.
        await harness.RaiseHumanAnswerAsync("activity-1");
        Assert.Empty(await harness.GetReservationsAsync());

        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.RaiseMachineAnswerAsync("activity-1");

        // Assert
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Empty(await harness.GetReservationsAsync());
        Assert.Null(interaction.AgentId);
        Assert.False(DialerCallMetadata.IsAgentClaimed(interaction));
        Assert.False(DialerCallMetadata.IsAbandoned(interaction));
        Assert.Empty(harness.Shared.Treatment.EndedWithMessage);
        Assert.DoesNotContain(harness.PublishedEvents, e => e.EventType is ContactCenterConstants.Events.DialerCallAbandoned or ContactCenterConstants.Events.DialerLiveAnswered);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task StatisticsUnavailable_PlacesNoCall()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(5);
        var profile = harness.CreateOverDialProfile();
        harness.Shared.Statistics.MakeUnavailable();

        // Act
        var started = await harness.RunPredictiveCycleAsync(profile);

        // Assert: the cap cannot be proven, so nothing is dialed, with or without an agent.
        Assert.Equal(0, started);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Empty(await harness.GetReservationsAsync());
        Assert.Equal(PredictivePacingDecisionMode.Suppressed, (await GetStateAsync(harness)).LastDecision.Mode);
    }

    [Theory]
    [InlineData(10, 0, PredictivePacingReason.AnswerRateSampleBelowFloor)]
    [InlineData(400, 5, PredictivePacingReason.ComplianceRateAtCap)]
    public async Task StatisticsBelowTheirFloorOrAtTheCap_FallBackToOneCallPerReservedAgent(int settledAttempts, int complianceAbandonedCalls, PredictivePacingReason reason)
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(5);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1, settledAttempts: settledAttempts, complianceAbandonedCalls: complianceAbandonedCalls);

        // Act
        var started = await harness.RunPredictiveCycleAsync(profile);

        // Assert: every call placed has its agent reserved, and none is in flight without one.
        var state = await GetStateAsync(harness);
        Assert.Equal(PredictivePacingDecisionMode.ReservedFallback, state.LastDecision.Mode);
        Assert.Equal(reason, state.LastDecision.Reason);
        Assert.True(started >= 1);
        Assert.All(harness.Router.PlacedCalls, call => Assert.False(string.IsNullOrEmpty(call.AgentId)));
        Assert.Equal(0, await harness.CountInFlightAsync());
    }

    [Fact]
    public async Task NoFreeAgent_PlacesNoCallWithoutAnAgent()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await SetPresenceAsync(harness, AgentOne, AgentPresenceStatus.Break);
        await harness.SeedQueuedActivitiesAsync(5);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);

        // Act
        var started = await harness.RunPredictiveCycleAsync(profile);

        // Assert
        Assert.Equal(0, started);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Equal(PredictivePacingReason.NoAgents, (await GetStateAsync(harness)).LastDecision.Reason);
    }

    [Fact]
    public async Task ReservationExpiryAndOrphanRecovery_LeaveCallsInFlightAlone()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(10);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        var started = await harness.RunPredictiveCycleAsync(profile);
        Assert.True(started > 0);

        // Act: long after any offer would have expired.
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        var expired = await harness.Services.GetRequiredService<IActivityReservationService>().ExpireDueAsync(TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        var recovery = ActivatorUtilities.CreateInstance<OrphanedActivityRecoveryService>(harness.Services, Mock.Of<IDialerAttemptFinalizer>());
        var dialed = harness.Router.PlacedCalls
            .Select(call => harness.Shared.Activities.Get(call.ActivityId))
            .ToArray();
        var recovered = await recovery.RecoverCandidatesAsync(dialed, harness.Clock.UtcNow, TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        // Assert: a call still ringing is not an expired offer nor an orphan.
        Assert.Equal(0, expired);
        Assert.Equal(0, recovered);
        Assert.Equal(started, await harness.CountInFlightAsync());
    }

    [Fact]
    public async Task TwoNodesPacingTheSameQueue_OnlyOneCycleCommits_AndTheLosersCallsAreNeverPlaced()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(busyTimeoutSeconds: 10, predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(20);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        var queueId = harness.PacingQueueId;
        var cancellationToken = TestContext.Current.CancellationToken;

        // The queue has been paced before, so it has its pacing record.
        await harness.Services.GetRequiredService<IPredictivePacingStateStore>()
            .CreateAsync(new PredictivePacingState { ItemId = "pacing-1", QueueId = queueId }, cancellationToken);
        await harness.Session.SaveChangesAsync(cancellationToken);

        // Two nodes, each with only a process-local lock, so neither keeps the other out. SQLite lets one transaction
        // write at a time, so the moment another node commits a cycle while this one is pacing is reproduced inside node
        // B's own transaction: right after node B reads the pacing record, the record's stored version moves on, exactly
        // as a cycle committed elsewhere would move it.
        var interleave = new InterleavedCycle(queueId);
        await using var nodeA = harness.OpenFlow(new FakeDistributedLock());
        await using var nodeB = harness.OpenFlow(new FakeDistributedLock(), services =>
            services.AddSingleton<IPredictivePacingStateStore>(sp => new InterleavingPacingStateStore(
                new PredictivePacingStateStore(sp.GetRequiredService<global::YesSql.ISession>()),
                sp.GetRequiredService<global::YesSql.ISession>(),
                interleave)));

        // Act: node A paces and commits; node B, more aggressive, paces while node A's cycle lands.
        var placedByA = await nodeA.Services.GetRequiredService<IDialerService>().RunCycleAsync(profile, queueId, cancellationToken);
        await nodeA.CommitAsync();

        var aggressive = harness.CreateOverDialProfile(p =>
        {
            p.MaxLinesPerAgent = 5;
            p.TargetAbandonmentRatePercent = 2.9;
        });
        var placedByB = await nodeB.Services.GetRequiredService<IDialerService>().RunCycleAsync(aggressive, queueId, cancellationToken);
        await nodeB.CommitAsync();

        Assert.True(interleave.Fired, "Node B never read the pacing record.");
        Assert.True(interleave.StagedCalls > 0, "Node B staged no call, so the race proved nothing.");

        // Assert: only node A's cycle committed, and only its calls were placed.
        await using var reader = harness.OpenFlow(new FakeDistributedLock());
        var state = await reader.Services.GetRequiredService<IPredictivePacingStateStore>().FindByQueueIdAsync(queueId, cancellationToken);
        var inFlight = await reader.Services.GetRequiredService<IQueueItemStore>().CountDialerInFlightAsync(queueId, cancellationToken);

        Assert.True(placedByA > 0);
        Assert.Equal(0, placedByB);
        Assert.Equal(1, state.Sequence);
        Assert.Equal(placedByA, inFlight);
        Assert.Equal(placedByA, harness.Router.PlacedCalls.Count);
    }

    [Fact]
    public async Task InboundOfferTakesTheAgentBetweenDialAndAnswer_TheAnsweredCallIsAbandoned()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));

        // An inbound call is offered to the only agent while the campaign call rings.
        var inbound = await harness.SeedQueuedActivityAsync(new OmnichannelActivity { ItemId = "inbound-1", PreferredDestination = "+15559990000" }, DialerModeIntegrationHarness.QueueId);
        var agent = await harness.AgentManager.FindByIdAsync(AgentOne, TestContext.Current.CancellationToken);
        var offer = await harness.Services.GetRequiredService<IActivityReservationService>().ReserveAsync(inbound, agent, 30, TestContext.Current.CancellationToken);
        Assert.NotNull(offer);

        // Act
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: the agent keeps the inbound offer, and the person who answered hears the message.
        Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(DialerAbandonment.Reasons.NoAgentAvailable, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Null(interaction.AgentId);

        var reservation = Assert.Single(await harness.GetReservationsAsync());
        Assert.Equal(offer.ItemId, reservation.ItemId);
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task AgentLegFailsAfterTheClaim_ThePersonHearsTheMessage_AndTheAgentGoesBackToWork()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));
        await harness.RaiseHumanAnswerAsync("activity-1");
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentOne));
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");

        // Act: the agent's phone never picks up.
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        var failed = await harness.Services.GetRequiredService<IContactCenterAgentLegFailureService>().FailAsync(
            DialerModeIntegrationHarness.ProviderName,
            interaction.ProviderInteractionId,
            HangupCause.Failed,
            TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        // Assert
        Assert.True(failed);
        var message = Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        Assert.Equal(interaction.ProviderInteractionId, message.CallId);

        interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        Assert.Equal(DialerAbandonment.Reasons.AgentLegFailed, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    private static async Task RunAnswerRaceAsync(int round)
    {
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(busyTimeoutSeconds: 10, predictive: true);
        var cancellationToken = TestContext.Current.CancellationToken;
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivitiesAsync(2);

        // A 6% target under a 10% cap lets one agent have two calls ringing at a 10% answer rate (an expected 5%).
        var profile = harness.CreateOverDialProfile(p =>
        {
            p.MaxAbandonmentRatePercent = 10;
            p.TargetAbandonmentRatePercent = 6;
        });
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(2, await harness.RunPredictiveCycleAsync(profile));

        var firstCallId = (await harness.FindInteractionByActivityAsync(harness.Router.PlacedCalls[0].ActivityId)).ProviderInteractionId;
        var secondCallId = (await harness.FindInteractionByActivityAsync(harness.Router.PlacedCalls[1].ActivityId)).ProviderInteractionId;

        // The harness's own unit of work lets go of the database before the flows run.
        await harness.Session.SaveChangesAsync(cancellationToken);

        var barrier = new BarrierDistributedLock("ContactCenterPredictiveConnect:", parties: 2);
        await using var first = harness.OpenFlow(barrier);
        await using var second = harness.OpenFlow(barrier);

        // Both people answer; each answer is its own delivery, whose connect runs once that delivery commits.
        await IngestAnswerAsync(harness, first, firstCallId, cancellationToken);
        await IngestAnswerAsync(harness, second, secondCallId, cancellationToken);

        // Act: both connects run at once.
        await Task.WhenAll(
            Task.Run(first.CommitAsync, cancellationToken),
            Task.Run(second.CommitAsync, cancellationToken))
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        // Assert, read back through a fresh unit of work.
        await using var reader = harness.OpenFlow(new FakeDistributedLock());
        var interactions = new List<Interaction>();

        foreach (var call in harness.Router.PlacedCalls)
        {
            interactions.Add(await reader.Services.GetRequiredService<IInteractionManager>().FindByActivityIdAsync(call.ActivityId, cancellationToken));
        }

        var reservations = (await reader.Services.GetRequiredService<IActivityReservationManager>()
            .GetActiveByAgentAsync(AgentOne, cancellationToken)).ToArray();
        var agent = await reader.Services.GetRequiredService<IAgentProfileManager>().FindByIdAsync(AgentOne, cancellationToken);

        Assert.True(interactions.Count(i => i.AgentId == AgentOne && DialerCallMetadata.IsAgentClaimed(i)) == 1, $"Round {round}: the agent was not claimed exactly once.");
        Assert.True(interactions.Count(i => DialerCallMetadata.GetAbandonedReason(i) == DialerAbandonment.Reasons.NoAgentAvailable) == 1, $"Round {round}: not exactly one call was abandoned.");
        Assert.True(harness.Shared.Treatment.EndedWithMessage.Count == 1, $"Round {round}: {harness.Shared.Treatment.EndedWithMessage.Count} abandoned-call messages were played.");
        Assert.True(reservations.Length == 1, $"Round {round}: the agent holds {reservations.Length} active reservations.");
        Assert.Equal(ReservationStatus.Accepted, reservations[0].Status);
        Assert.Equal(AgentPresenceStatus.Busy, agent.PresenceStatus);
    }

    private static async Task IngestAnswerAsync(DialerModeIntegrationHarness harness, HarnessFlow flow, string providerCallId, CancellationToken cancellationToken)
    {
        await flow.Services.GetRequiredService<IProviderVoiceEventService>().IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderCallId = providerCallId,
            State = VoiceCallState.Connected,
            OccurredUtc = harness.Clock.UtcNow,
            IdempotencyKey = $"{providerCallId}:connected",
        }, cancellationToken);
    }

    private sealed class InterleavedCycle
    {
        public InterleavedCycle(string queueId)
        {
            QueueId = queueId;
        }

        public string QueueId { get; }

        public bool Fired { get; set; }

        public int StagedCalls { get; set; }
    }

    // Reads the pacing record, then moves its stored version on inside the reading transaction, as a cycle committed by
    // another node between this node's read and its write would. It also notes how many calls the cycle had staged.
    private sealed class InterleavingPacingStateStore : IPredictivePacingStateStore
    {
        private readonly PredictivePacingStateStore _inner;
        private readonly global::YesSql.ISession _session;
        private readonly InterleavedCycle _interleave;

        public InterleavingPacingStateStore(PredictivePacingStateStore inner, global::YesSql.ISession session, InterleavedCycle interleave)
        {
            _inner = inner;
            _session = session;
            _interleave = interleave;
        }

        public async Task<PredictivePacingState> FindByQueueIdAsync(string queueId, CancellationToken cancellationToken = default)
        {
            var state = await _inner.FindByQueueIdAsync(queueId, cancellationToken);

            if (!_interleave.Fired && state is not null)
            {
                _interleave.Fired = true;
                _interleave.StagedCalls = await _session.Query<QueueItem, CrestApps.OrchardCore.ContactCenter.Core.Indexes.QueueItemIndex>(
                    index => index.QueueId == queueId && index.Status == QueueItemStatus.Assigned && index.AgentId == null,
                    collection: ContactCenterStorage.CollectionName).CountAsync(cancellationToken);

                var transaction = await _session.BeginTransactionAsync(cancellationToken);
                await using var command = transaction.Connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {_session.Store.Configuration.TablePrefix}{ContactCenterStorage.CollectionName}_Document SET Version = Version + 1 WHERE Content LIKE '%\"QueueId\":\"{queueId}\"%' AND Type LIKE '%PredictivePacingState%'";
                Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
            }

            return state;
        }

        public ValueTask<bool> DeleteAsync(PredictivePacingState entry, CancellationToken cancellationToken = default) => _inner.DeleteAsync(entry, cancellationToken);

        public ValueTask<PredictivePacingState> FindByIdAsync(string id, CancellationToken cancellationToken = default) => _inner.FindByIdAsync(id, cancellationToken);

        public ValueTask<IReadOnlyCollection<PredictivePacingState>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) => _inner.GetAsync(ids, cancellationToken);

        public ValueTask<IReadOnlyCollection<PredictivePacingState>> GetAllAsync(CancellationToken cancellationToken = default) => _inner.GetAllAsync(cancellationToken);

        public ValueTask<CrestApps.Core.Models.PageResult<PredictivePacingState>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
            where TQuery : CrestApps.Core.Models.QueryContext
            => _inner.PageAsync(page, pageSize, context, cancellationToken);

        public ValueTask CreateAsync(PredictivePacingState record, CancellationToken cancellationToken = default) => _inner.CreateAsync(record, cancellationToken);

        public ValueTask UpdateAsync(PredictivePacingState record, CancellationToken cancellationToken = default) => _inner.UpdateAsync(record, cancellationToken);
    }

    private static Task<PredictivePacingState> GetStateAsync(DialerModeIntegrationHarness harness)
        => harness.Services.GetRequiredService<IPredictivePacingStateStore>().FindByQueueIdAsync(harness.PacingQueueId, TestContext.Current.CancellationToken);

    private static async Task SetLastAssignedAsync(DialerModeIntegrationHarness harness, string agentId, DateTime lastAssignedUtc)
    {
        var agent = await harness.AgentManager.FindByIdAsync(agentId, TestContext.Current.CancellationToken);
        agent.LastAssignedUtc = lastAssignedUtc;
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SetPresenceAsync(DialerModeIntegrationHarness harness, string agentId, AgentPresenceStatus status)
    {
        var agent = await harness.AgentManager.FindByIdAsync(agentId, TestContext.Current.CancellationToken);
        agent.PresenceStatus = status;
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
