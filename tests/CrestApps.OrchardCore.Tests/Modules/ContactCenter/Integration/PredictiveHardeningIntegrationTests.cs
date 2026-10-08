using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// The latency and compliance safeguards around over-dialing, end to end on the dialer harness: the deadline that gives
/// up on a claimed agent whose leg does not answer, the sweep that settles answered calls nothing connected, the connect
/// wait held to the measured connect time, retries of abandoned calls carried across follow-up activities, the retry of a
/// cycle that found its pacing lock held, and discounting agents owed to inbound queues.
/// </summary>
public sealed class PredictiveHardeningIntegrationTests
{
    private const string AgentOne = "agent-1";
    private const string AgentTwo = "agent-2";

    [Fact]
    public async Task ClaimedAgentsLegDoesNotAnswer_TheDeadlineReleasesTheAgent_AndThePersonHearsTheMessage()
    {
        // Arrange
        await using var harness = await CreateClaimedCallAsync();
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        var deadlineKey = PredictiveAgentConnector.GetAgentLegDeadlineKey(interaction.ItemId);
        var reservation = Assert.Single(await harness.GetReservationsAsync());

        // The claim armed the deadline, and the agent's leg is tagged so a phone standing by answers it at once.
        Assert.Contains(deadlineKey, harness.Shared.Deadlines.Keys);
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Contains($"\"StandbyReservationId\":\"{reservation.ItemId}\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains("\"AgentLegTimeoutSeconds\":5", answer.RequestPayload, StringComparison.Ordinal);

        // Act: the agent's phone never picks up.
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.Shared.Deadlines.RunAsync(deadlineKey, harness.Services);
        await harness.CommitAsync();

        // Assert: the person hears the message, the call is abandoned as a timeout, and the agent is back at work.
        var message = Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        Assert.Equal(interaction.ProviderInteractionId, message.CallId);

        interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        Assert.Equal(DialerAbandonment.Reasons.AgentLegTimeout, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
        Assert.Contains(harness.PacingQueueId, harness.Shared.Pacing.Requests);

        // Given up on once: a second deadline, the sweep or the agent's late answer changes nothing.
        var connector = harness.Services.GetRequiredService<IPredictiveAgentConnector>();
        Assert.False(await connector.ReleaseUnansweredAgentLegAsync(interaction.ItemId, TestContext.Current.CancellationToken));
        await harness.RaiseAgentLegAnsweredAsync("activity-1");
        Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task AgentJoinsBeforeTheDeadline_TheDeadlineChangesNothing()
    {
        // Arrange
        await using var harness = await CreateClaimedCallAsync();
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        await harness.RaiseAgentLegAnsweredAsync("activity-1");

        // Act
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.Shared.Deadlines.RunAsync(PredictiveAgentConnector.GetAgentLegDeadlineKey(interaction.ItemId), harness.Services);
        await harness.CommitAsync();

        // Assert
        Assert.Empty(harness.Shared.Treatment.EndedWithMessage);
        interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.False(interaction.IsSettled);
        Assert.False(DialerCallMetadata.IsAbandoned(interaction));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task ClaimedAgentNeverJoined_AndTheDeadlineWasLost_TheSweepReleasesTheAgentAfterTheSweepDelay()
    {
        // Arrange: the node that held the deadline stopped.
        await using var harness = await CreateClaimedCallAsync();
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        harness.Shared.Deadlines.Cancel(PredictiveAgentConnector.GetAgentLegDeadlineKey(interaction.ItemId));
        var connector = harness.Services.GetRequiredService<IPredictiveAgentConnector>();

        // Act and assert: within the agent-leg timeout plus the sweep delay the sweep leaves the call alone.
        harness.Clock.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(0, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));
        Assert.Empty(harness.Shared.Treatment.EndedWithMessage);

        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));
        await harness.CommitAsync();

        Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(DialerAbandonment.Reasons.AgentLegTimeout, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));

        // Settled once.
        Assert.Equal(0, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnsweredCallNothingConnected_TheSweepPlaysTheMessageAfterTheSweepDelay()
    {
        // Arrange: a profile that waits for an agent, with connects measured fast enough to allow the wait; the only agent
        // is on a break when the person answers, and the node holding the connect retries stops.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile(p => p.ConnectWaitMilliseconds = 1000);
        harness.SeedPacingStats(answerRate: 0.1);
        harness.Shared.Statistics.SeedConnectLatency(TimeSpan.FromMilliseconds(500), samples: 50);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));
        await SetPresenceAsync(harness, AgentOne, AgentPresenceStatus.Break);

        await harness.RaiseHumanAnswerAsync("activity-1");
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");

        // The call is waiting for an agent: no message yet, and a connect retry was armed (and is lost with its node).
        Assert.Empty(harness.Shared.Treatment.EndedWithMessage);
        Assert.Contains(PredictiveAgentConnector.GetConnectDeadlineKey(interaction.ItemId), harness.Shared.Deadlines.Keys);
        harness.Shared.Deadlines.Cancel(PredictiveAgentConnector.GetConnectDeadlineKey(interaction.ItemId));
        var connector = harness.Services.GetRequiredService<IPredictiveAgentConnector>();

        // Act and assert: before the sweep delay the call is left alone; after it, the person hears the message.
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(0, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));

        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));
        await harness.CommitAsync();

        var message = Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        Assert.Equal(interaction.ProviderInteractionId, message.CallId);
        interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(DialerAbandonment.Reasons.AnsweredUnconnected, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.Contains(harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.DialerCallAbandoned && e.InteractionId == interaction.ItemId);
        Assert.Equal(AgentPresenceStatus.Break, await harness.GetPresenceAsync(AgentOne));

        Assert.Equal(0, await connector.SweepAnsweredUnconnectedAsync(TestContext.Current.CancellationToken));
        Assert.Single(harness.Shared.Treatment.EndedWithMessage);
    }

    [Fact]
    public async Task ConnectWait_WithoutMeasuredConnectTimes_DoesNotWait()
    {
        // Arrange: the profile asks to wait, but nothing says an agent could still be connected within two seconds.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile(p => p.ConnectWaitMilliseconds = 1000);
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));
        await SetPresenceAsync(harness, AgentOne, AgentPresenceStatus.Break);

        // Act
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: the message plays at once.
        Assert.Single(harness.Shared.Treatment.EndedWithMessage);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(DialerAbandonment.Reasons.NoAgentAvailable, DialerCallMetadata.GetAbandonedReason(interaction));
        Assert.DoesNotContain(PredictiveAgentConnector.GetConnectDeadlineKey(interaction.ItemId), harness.Shared.Deadlines.Keys);
    }

    [Fact]
    public async Task FollowUpOfAnAbandonedCall_IsDialedOnlyWithAnAgentReservedForIt()
    {
        // Arrange: the follow-up a disposition created for a contact whose last call was abandoned, queued with the mark,
        // and a fresh record behind it.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        var followUp = await harness.SeedQueuedActivityAsync(new OmnichannelActivity { ItemId = "follow-up-1", PreferredDestination = "+15551230001" }, harness.PacingQueueId);
        await MarkRequiresReservedAgentAsync(harness, followUp);
        await harness.SeedQueuedActivityAsync("activity-2", "+15551230002");
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);

        // Act
        await harness.RunPredictiveCycleAsync(profile);

        // Assert: the follow-up was placed for an agent reserved for it; the fresh record without one.
        var retry = Assert.Single(harness.Router.PlacedCalls, call => call.ActivityId == "follow-up-1");
        Assert.False(string.IsNullOrEmpty(retry.AgentId));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(retry.AgentId));

        var fresh = Assert.Single(harness.Router.PlacedCalls, call => call.ActivityId == "activity-2");
        Assert.Null(fresh.AgentId);
    }

    [Fact]
    public async Task FollowUpOfAnAbandonedCall_WhenTheProfileDoesNotRequireAnAgent_IsOverDialed()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        var followUp = await harness.SeedQueuedActivityAsync(new OmnichannelActivity { ItemId = "follow-up-1", PreferredDestination = "+15551230001" }, harness.PacingQueueId);
        await MarkRequiresReservedAgentAsync(harness, followUp);
        var profile = harness.CreateOverDialProfile(p => p.AbandonedRetryRequiresAgent = false);
        harness.SeedPacingStats(answerRate: 0.1);

        // Act
        await harness.RunPredictiveCycleAsync(profile);

        // Assert
        var call = Assert.Single(harness.Router.PlacedCalls);
        Assert.Null(call.AgentId);
        Assert.Empty(await harness.GetReservationsAsync());
    }

    [Fact]
    public async Task PacingLockHeld_PlacesNothing_AndAsksForTheQueueToBePacedAgainShortly()
    {
        // Arrange: another cycle holds the queue's pacing lock.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        await harness.SeedQueuedActivitiesAsync(5);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        await using var node = harness.OpenFlow(new PacingLockHeldElsewhere());

        // Act
        var placed = await node.Services.GetRequiredService<IDialerService>().RunCycleAsync(profile, harness.PacingQueueId, TestContext.Current.CancellationToken);
        await node.CommitAsync();

        // Assert
        Assert.Equal(0, placed);
        Assert.Empty(harness.Router.PlacedCalls);
        Assert.Equal(new[] { harness.PacingQueueId }, harness.Shared.Pacing.Retries.ToArray());
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task AgentsOwedToAnInboundQueueWithCallsWaiting_AreDiscountedOnlyWhenConfigured(bool discount, int countedAgents)
    {
        // Arrange: agent-2 is also signed in to an inbound queue with a caller waiting.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentsAsync(2);
        var agent = await harness.AgentManager.FindByIdAsync(AgentTwo, TestContext.Current.CancellationToken);
        agent.QueueIds = [harness.PacingQueueId, DialerModeIntegrationHarness.QueueId];
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.SeedQueuedActivityAsync(new OmnichannelActivity { ItemId = "inbound-1", PreferredDestination = "+15559990000" }, DialerModeIntegrationHarness.QueueId);
        await harness.SeedQueuedActivitiesAsync(10);
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        await using var node = harness.OpenFlow(new FakeDistributedLock(), services =>
            services.Configure<ContactCenterPredictiveDialingOptions>(options => options.DiscountAgentsWithWaitingInbound = discount));

        // Act
        await node.Services.GetRequiredService<IDialerService>().RunCycleAsync(profile, harness.PacingQueueId, TestContext.Current.CancellationToken);
        await node.CommitAsync();

        // Assert
        await using var reader = harness.OpenFlow(new FakeDistributedLock());
        var state = await reader.Services.GetRequiredService<IPredictivePacingStateStore>().FindByQueueIdAsync(harness.PacingQueueId, TestContext.Current.CancellationToken);
        Assert.Equal(countedAgents, state.LastDecision.AvailableAgents);
    }

    // One agent, one call placed without an agent, a person answers, and the agent is claimed.
    private static async Task<DialerModeIntegrationHarness> CreateClaimedCallAsync()
    {
        var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = harness.CreateOverDialProfile();
        harness.SeedPacingStats(answerRate: 0.1);
        Assert.Equal(1, await harness.RunPredictiveCycleAsync(profile));
        await harness.RaiseHumanAnswerAsync("activity-1");
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentOne));

        return harness;
    }

    private static async Task MarkRequiresReservedAgentAsync(DialerModeIntegrationHarness harness, QueueItem item)
    {
        var manager = harness.Services.GetRequiredService<IQueueItemManager>();
        var current = await manager.FindByIdAsync(item.ItemId, TestContext.Current.CancellationToken);
        current.RequiresReservedAgent = true;
        await manager.UpdateAsync(current, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SetPresenceAsync(DialerModeIntegrationHarness harness, string agentId, AgentPresenceStatus status)
    {
        var agent = await harness.AgentManager.FindByIdAsync(agentId, TestContext.Current.CancellationToken);
        agent.PresenceStatus = status;
        await harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Grants every key except a campaign's pacing lock, which another cycle holds.
    private sealed class PacingLockHeldElsewhere : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new NoLocker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>((new NoLocker(), !key.StartsWith("ContactCenterPredictivePacing:", StringComparison.Ordinal)));

        public Task<bool> IsLockAcquiredAsync(string key) => Task.FromResult(false);

        private sealed class NoLocker : ILocker
        {
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
