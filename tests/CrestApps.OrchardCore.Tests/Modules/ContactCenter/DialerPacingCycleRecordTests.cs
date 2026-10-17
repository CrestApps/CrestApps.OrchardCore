using System.Collections.Concurrent;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every way a pacing cycle can end leaves a record of how it ended, written apart from the cycle's own transaction, so a
/// cycle that is rolled back, abandoned at its time budget or that throws is not indistinguishable from one that never ran.
/// </summary>
public sealed class DialerPacingCycleRecordTests
{
    private static readonly string _queue = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

    private static readonly ContactCenterDialerPacingOptions _options = new()
    {
        Debounce = TimeSpan.FromMilliseconds(300),
        LockExpiration = TimeSpan.FromSeconds(30),
        LockRetryDelay = TimeSpan.FromSeconds(1),
        CycleTimeout = TimeSpan.FromMilliseconds(200),
    };

    [Fact]
    public async Task ACycleThatFindsNoWaitingRecord_IsRecordedAsNothingDue()
    {
        // Arrange
        var fixture = new Fixture { HeadWaiting = false };

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.NothingDue, cycle.Outcome);
        Assert.Equal(_queue, cycle.QueueId);
        fixture.DialerService.Verify(
            service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ACycleThatPlacesCalls_IsRecordedAsPlaced_WithTheLastCallItPlacedAndAssigned()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.OnCycle(async publisher =>
        {
            await publisher.PublishAsync(RoutingDecision(succeeded: true, activityItemId: "activity-1", agentId: "agent-1"));
            await publisher.PublishAsync(AttemptStarted("interaction-1"));
            await publisher.PublishAsync(RoutingDecision(succeeded: true, activityItemId: "activity-2", agentId: "agent-2"));
            await publisher.PublishAsync(AttemptStarted("interaction-2"));

            return 2;
        });

        fixture.Interactions["interaction-2"] = new Interaction { ItemId = "interaction-2", ActivityItemId = "activity-2", AgentId = "agent-2" };

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.Placed, cycle.Outcome);
        Assert.Equal(2, cycle.CallsPlaced);
        Assert.Equal(2, cycle.RoutingSucceeded);
        Assert.Equal("dp1", cycle.DialerProfileId);
        Assert.Equal("interaction-2", cycle.LastPlacedInteractionId);
        Assert.Equal("activity-2", cycle.LastPlacedActivityItemId);
        Assert.Equal("agent-2", cycle.LastPlacedAgentId);
        Assert.Equal("agent-2", cycle.LastAssignedAgentId);
        Assert.Equal("activity-2", cycle.LastAssignedActivityItemId);
    }

    [Fact]
    public async Task ACycleWhoseRoutingFindsNoAgent_IsRecordedAsNoAgents()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.OnCycle(async publisher =>
        {
            await publisher.PublishAsync(RoutingDecision(succeeded: false));

            return 0;
        });

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.NoAgents, cycle.Outcome);
        Assert.Equal(1, cycle.RoutingNoAgent);
        Assert.Equal(0, cycle.CallsPlaced);
    }

    [Fact]
    public async Task RoutingDecisionsFoldedIntoOneEvent_AreCountedByTheirCount()
    {
        // Arrange: one event standing for five identical "no agent" decisions, as a coalescing decision log writes it.
        var fixture = new Fixture();
        fixture.OnCycle(async publisher =>
        {
            await publisher.PublishAsync(RoutingDecision(succeeded: false, count: 5));
            await publisher.PublishAsync(RoutingDecision(succeeded: false));

            return 0;
        });

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.NoAgents, cycle.Outcome);
        Assert.Equal(6, cycle.RoutingNoAgent);
    }

    [Fact]
    public async Task ACycleWhoseAttemptsAreSuppressed_IsRecordedAsHeldBack_WithTheReason()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.OnCycle(async publisher =>
        {
            await publisher.PublishAsync(RoutingDecision(succeeded: true, activityItemId: "activity-1", agentId: "agent-1"));
            await publisher.PublishAsync(Suppression(DialerSuppressionReason.DoNotCall));

            return 0;
        });

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.HeldBack, cycle.Outcome);
        Assert.Equal(1, cycle.Suppressed);
        Assert.Contains(nameof(DialerSuppressionReason.DoNotCall), cycle.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APredictiveCycleTheAbandonmentPolicyStops_IsRecordedAsHeldBack()
    {
        // Arrange: the policy refuses before routing is ever asked for an agent.
        var fixture = new Fixture();
        fixture.Profile.Mode = DialerMode.Predictive;
        fixture.Profile.PredictivePacingModel = PredictivePacingModel.ReservedPerCall;
        fixture.Policy
            .Setup(policy => policy.EvaluateAsync(fixture.Profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerAbandonmentEvaluation.Suppressed(true, 4.2, 100, "over the cap"));
        fixture.OnCycle(_ => Task.FromResult(0));

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.HeldBack, cycle.Outcome);
        Assert.Contains("abandonment policy", cycle.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACycleWithNothingDue_IsRecordedAsNothingDue_SoItIsToldApartFromOneWithNoAgent()
    {
        // Arrange: the head record is scheduled for later, so routing is never asked for an agent.
        var fixture = new Fixture();
        fixture.OnCycle(_ => Task.FromResult(0));

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.NothingDue, cycle.Outcome);
    }

    [Fact]
    public async Task ACycleThatFindsThePacingLockHeld_IsRecordedAsLockNotAcquired()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Lock.Hold(DialerPacingScheduler.GetPacingLockKey(_queue));

        // Act
        await fixture.RunAsync();

        // Assert
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.LockNotAcquired, cycle.Outcome);
    }

    [Fact]
    public async Task ACycleThatThrows_IsRolledBack_AndStillRecordedAsFailed()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.OnCycle(_ => throw new InvalidOperationException("The provider refused the call."));

        // Act
        await fixture.RunAsync();

        // Assert
        fixture.Session.Verify(session => session.CancelAsync(), Times.Once);
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.Failed, cycle.Outcome);
        Assert.Contains("The provider refused the call.", cycle.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACycleThatLosesAWriteRace_IsRolledBack_AndStillRecordedAsAWriteConflict()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.OnCycle(_ => Task.FromResult(1));
        fixture.Session
            .Setup(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));

        // Act
        await fixture.RunAsync();

        // Assert
        fixture.Session.Verify(session => session.CancelAsync(), Times.Once);
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.WriteConflict, cycle.Outcome);
    }

    [Fact]
    public async Task ACycleCutShortByItsTimeBudget_IsRolledBack_AndStillRecordedAsTimeBudgetExceeded()
    {
        // Arrange: the cycle waits on something that never answers.
        var fixture = new Fixture();
        fixture.OnCycle(_ => new TaskCompletionSource<int>().Task);

        // Act
        await fixture.RunAsync();

        // Assert
        fixture.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        var cycle = Assert.Single(fixture.Recorder.Cycles);
        Assert.Equal(DialerPacingCycleOutcome.TimeBudgetExceeded, cycle.Outcome);
    }

    [Fact]
    public async Task OutsideACycle_PublishedEventsAreNotCounted()
    {
        // Arrange
        var observation = new DialerPacingCycleObservation();
        var inner = new Mock<IContactCenterEventPublisher>();
        var publisher = new DialerPacingCycleObservingEventPublisher(inner.Object, observation);

        // Act
        await publisher.PublishAsync(RoutingDecision(succeeded: false), TestContext.Current.CancellationToken);
        observation.Begin();
        await publisher.PublishAsync(RoutingDecision(succeeded: false), TestContext.Current.CancellationToken);
        observation.End();
        await publisher.PublishAsync(RoutingDecision(succeeded: false), TestContext.Current.CancellationToken);

        // Assert: every event still reaches the publisher; only the one inside the cycle is counted.
        inner.Verify(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal(1, observation.RoutingNoAgent);
    }

    internal static InteractionEvent RoutingDecision(bool succeeded, string activityItemId = "activity-1", string agentId = null, int? count = null)
    {
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.RoutingDecisionMade };

        interactionEvent.SetData(new
        {
            QueueId = _queue,
            ActivityItemId = activityItemId,
            SelectedAgentId = agentId,
            Succeeded = succeeded,
            Reason = succeeded ? null : "No eligible agent is available.",
            Count = count,
        });

        return interactionEvent;
    }

    private static InteractionEvent AttemptStarted(string interactionId)
        => new()
        {
            EventType = ContactCenterConstants.Events.DialerAttemptStarted,
            InteractionId = interactionId,
        };

    private static InteractionEvent Suppression(DialerSuppressionReason reason)
    {
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.DialSuppressed };

        interactionEvent.SetData(new DialerSuppressionEventData
        {
            ProfileItemId = "dp1",
            ActivityItemId = "activity-1",
            Reason = reason,
        });

        return interactionEvent;
    }

    /// <summary>
    /// A campaign whose waiting work was loaded under a Power profile, paced by the real scheduler with a recorder that
    /// keeps every record and the real observation behind the event publisher.
    /// </summary>
    private sealed class Fixture
    {
        private Func<IContactCenterEventPublisher, Task<int>> _cycle = _ => Task.FromResult(0);

        public Fixture()
        {
            // The policy permits dialing unless a test says otherwise.
            Policy
                .Setup(policy => policy.EvaluateAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DialerAbandonmentEvaluation.Permitted(true, 0, 100, "under the cap"));
        }

        public bool HeadWaiting { get; init; } = true;

        public DialerProfile Profile { get; } = new()
        {
            ItemId = "dp1",
            Name = "power",
            Enabled = true,
            Mode = DialerMode.Power,
            CallsPerAgent = 1,
        };

        public Mock<IDialerService> DialerService { get; } = new();

        public Mock<ISession> Session { get; } = new();

        public Mock<IDialerAbandonmentPolicyService> Policy { get; } = new();

        public HoldableLock Lock { get; } = new();

        public RecordingDialerPacingCycleRecorder Recorder { get; } = new();

        public Dictionary<string, Interaction> Interactions { get; } = new(StringComparer.Ordinal);

        public void OnCycle(Func<IContactCenterEventPublisher, Task<int>> cycle)
            => _cycle = cycle;

        public async Task RunAsync()
        {
            var clock = new TestClock();
            var scheduler = new DialerPacingScheduler(
                Mock.Of<IContactCenterDeadlineScheduler>(),
                clock,
                Options.Create(_options),
                NullLogger<DialerPacingScheduler>.Instance);

            await using var services = BuildServices();

            await scheduler.RunAsync(services, _queue, TestContext.Current.CancellationToken);
        }

        private ServiceProvider BuildServices()
        {
            var observation = new DialerPacingCycleObservation();
            var publisher = new DialerPacingCycleObservingEventPublisher(Mock.Of<IContactCenterEventPublisher>(), observation);

            var queueItemStore = new Mock<IQueueItemStore>();
            queueItemStore
                .Setup(store => store.FindNextWaitingAsync(_queue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(HeadWaiting ? new QueueItem { QueueId = _queue, ActivityItemId = "activity-1", DialerProfileId = Profile.ItemId } : null);

            var profileReader = new Mock<IDialerProfileReader>();
            profileReader
                .Setup(reader => reader.FindByIdAsync(Profile.ItemId, It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(Profile));

            DialerService
                .Setup(service => service.RunCycleAsync(Profile, _queue, It.IsAny<CancellationToken>()))
                .Returns(() => _cycle(publisher));

            var interactionStore = new Mock<IInteractionStore>();
            interactionStore
                .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken _) => ValueTask.FromResult(Interactions.GetValueOrDefault(id)));

            var services = new ServiceCollection();
            services.AddSingleton(queueItemStore.Object);
            services.AddSingleton(profileReader.Object);
            services.AddSingleton(DialerService.Object);
            services.AddSingleton(Session.Object);
            services.AddSingleton(Policy.Object);
            services.AddSingleton(interactionStore.Object);
            services.AddSingleton<IDistributedLock>(Lock);
            services.AddSingleton<IContactCenterFeatureWorkManager>(new TestContactCenterFeatureWorkManager());
            services.AddSingleton<IDialerPacingCycleRecorder>(Recorder);
            services.AddSingleton(observation);

            return services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// A lock one holder at a time can take, which a test can hold as another node would.
    /// </summary>
    private sealed class HoldableLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, byte> _held = new(StringComparer.Ordinal);

        public void Hold(string key) => _held.TryAdd(key, 0);

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => throw new NotSupportedException();

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>(_held.TryAdd(key, 0) ? (new Locker(this, key), true) : (null, false));

        public Task<bool> IsLockAcquiredAsync(string key) => Task.FromResult(_held.ContainsKey(key));

        private sealed class Locker : ILocker
        {
            private readonly HoldableLock _owner;
            private readonly string _key;

            public Locker(HoldableLock owner, string key)
            {
                _owner = owner;
                _key = key;
            }

            public void Dispose() => _owner._held.TryRemove(_key, out _);

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
        }
    }
}
