using System.Collections.Concurrent;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Campaigns that reserve an agent before each call are paced as soon as an agent may be free or work arrives: the event
/// asks for the campaign's queue, requests for one queue are merged into one debounced run, and the run paces the queue
/// under its pacing lock, so two runs never pace one campaign at once.
/// </summary>
public sealed class DialerPacingSchedulerTests
{
    private static readonly string _campaignQueue = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

    private static readonly ContactCenterDialerPacingOptions _options = new()
    {
        Debounce = TimeSpan.FromMilliseconds(300),
        LockExpiration = TimeSpan.FromSeconds(30),
        LockRetryDelay = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public void AnAgentBecomingAvailable_RequestsTheCampaignsTheyAreSignedInto()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentStateChanged };
        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = "agent-1",
            CurrentState = AgentPresenceStatus.Available,
            QueueIds = ["queue-1", _campaignQueue],
            CampaignIds = ["campaign-1", "campaign-2"],
        });

        // Act
        var queueIds = DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent);

        // Assert: each campaign once, never an inbound queue.
        Assert.Equal(
            [_campaignQueue, ContactCenterConstants.CampaignQueue.CreateId("campaign-2")],
            queueIds.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.AgentSignedIn)]
    [InlineData(ContactCenterConstants.Events.AgentPresenceChanged)]
    public void AnAgentSigningInOrSettingAvailable_RequestsTheirCampaigns(string eventType)
    {
        // Arrange: signing into a campaign while already available changes no state, so only the presence event says so.
        var interactionEvent = new InteractionEvent { EventType = eventType };
        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = AgentPresenceStatus.Available,
            CurrentStatus = AgentPresenceStatus.Available,
            CampaignIds = ["campaign-1"],
        });

        // Act + Assert
        Assert.Equal([_campaignQueue], DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Fact]
    public void AnAgentGoingOnBreak_RequestsNothing()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentPresenceChanged };
        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            CurrentStatus = AgentPresenceStatus.Break,
            CampaignIds = ["campaign-1"],
        });

        // Act + Assert
        Assert.Empty(DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.DialerAttemptCompleted)]
    [InlineData(ContactCenterConstants.Events.CallEnded)]
    public void ACallThatEnded_RequestsItsCampaign(string eventType)
    {
        // Arrange: a no-answer, busy, machine or failed attempt frees the agent it reserved.
        var interactionEvent = new InteractionEvent { EventType = eventType };
        interactionEvent.SetData(new CallLifecycleEventData { CampaignId = "campaign-1" });

        // Act + Assert
        Assert.Equal([_campaignQueue], DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Fact]
    public void AReleasedOffer_RequestsItsCampaign_ButNotAnInboundQueue()
    {
        // Arrange
        var campaignEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentReleased };
        campaignEvent.SetData(new OfferLifecycleEventData { QueueId = _campaignQueue });
        var inboundEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentReleased };
        inboundEvent.SetData(new OfferLifecycleEventData { QueueId = "queue-1" });

        // Act + Assert
        Assert.Equal([_campaignQueue], DialerPacingTriggerHandler.ResolveQueueIds(campaignEvent));
        Assert.Empty(DialerPacingTriggerHandler.ResolveQueueIds(inboundEvent));
    }

    [Fact]
    public void ARecordLoadedIntoACampaign_RequestsItsQueue()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.QueueItemAdded };
        interactionEvent.SetData(new QueueItemAddedEventData { QueueItemId = "item-1", QueueId = _campaignQueue, DialerProfileId = "dp1" });

        // Act + Assert
        Assert.Equal([_campaignQueue], DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.QueueItemAssigned)]
    [InlineData(ContactCenterConstants.Events.CallConnected)]
    public void AnEventThatOnlyTakesAnAgent_RequestsNothing(string eventType)
    {
        // Arrange: a cycle's own reservations raise these; asking again on them would set off cycle after cycle.
        var interactionEvent = new InteractionEvent { EventType = eventType };
        interactionEvent.SetData(new OfferLifecycleEventData { QueueId = _campaignQueue });

        // Act + Assert
        Assert.Empty(DialerPacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Fact]
    public void ABurstOfRequests_IsMergedIntoOneRun_DueAfterTheDebounce()
    {
        // Arrange
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);

        // Act: several agents free up at once, and an inbound queue is asked for too.
        scheduler.Request(_campaignQueue);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        scheduler.Request(_campaignQueue);
        scheduler.Request(_campaignQueue);
        scheduler.Request("queue-1");

        // Assert: one run, not pushed back by the later requests, and never for an inbound queue.
        var scheduled = Assert.Single(deadlines.Scheduled);
        Assert.Equal(DialerPacingScheduler.GetDeadlineKey(_campaignQueue), scheduled.Key);
        Assert.Equal(clock.UtcNow.AddMilliseconds(-100).Add(_options.Debounce), scheduled.DueUtc);
    }

    [Fact]
    public async Task ARequestArrivingWhileARunPaces_ArmsTheNextRun()
    {
        // Arrange
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);
        var fixture = new RunFixture();
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .Callback(() => scheduler.Request(_campaignQueue))
            .ReturnsAsync(0);

        scheduler.Request(_campaignQueue);

        // Act
        await deadlines.RunAsync(DialerPacingScheduler.GetDeadlineKey(_campaignQueue), fixture.BuildServices());

        // Assert: the change seen during the run is not lost in the merge.
        Assert.Equal(2, deadlines.Scheduled.Count);
    }

    [Fact]
    public async Task AnAgentBecomingAvailable_PacesTheCampaignWithItsProfile_UnderItsPacingLock()
    {
        // Arrange
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);
        var handler = new DialerPacingTriggerHandler(scheduler);
        var fixture = new RunFixture();
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(fixture.Lock.IsHeld(DialerPacingScheduler.GetPacingLockKey(_campaignQueue))))
            .ReturnsAsync(0);

        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentStateChanged };
        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = "agent-1",
            CurrentState = AgentPresenceStatus.Available,
            CampaignIds = ["campaign-1"],
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);
        await deadlines.RunAsync(DialerPacingScheduler.GetDeadlineKey(_campaignQueue), fixture.BuildServices());

        // Assert: the same cycle the minute task runs, committed before the lock is let go.
        fixture.DialerService.Verify(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(fixture.Lock.IsHeld(DialerPacingScheduler.GetPacingLockKey(_campaignQueue)));
    }

    [Fact]
    public async Task ADialerAttemptThatDidNotConnect_PacesTheCampaignAgain_AndKeepsPacingWhileCallsArePlaced()
    {
        // Arrange
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);
        var handler = new DialerPacingTriggerHandler(scheduler);
        var fixture = new RunFixture();
        fixture.DialerService
            .SetupSequence(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ReturnsAsync(0);

        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.DialerAttemptCompleted };
        interactionEvent.SetData(new CallLifecycleEventData { QueueId = _campaignQueue, Reason = "no-answer" });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);
        var key = DialerPacingScheduler.GetDeadlineKey(_campaignQueue);
        var first = await deadlines.RunAsync(key, fixture.BuildServices());

        // The deadline scheduler runs the work again at the time it returned.
        var second = await scheduler.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);

        // Assert: a cycle that placed a call asks to run again after the debounce, so agents freed together are each
        // dialed; one that placed nothing waits for the next event.
        Assert.Equal(clock.UtcNow.Add(_options.Debounce), first);
        Assert.Null(second);
        fixture.DialerService.Verify(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ARunThatFindsThePacingLockHeld_PlacesNothing_AndTriesAgainAfterTheRetryDelay()
    {
        // Arrange: another node, or the run a moment ago, is pacing this campaign.
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);
        var fixture = new RunFixture();
        await using var held = await fixture.Lock.AcquireLockAsync(DialerPacingScheduler.GetPacingLockKey(_campaignQueue));

        scheduler.Request(_campaignQueue);

        // Act
        await deadlines.RunAsync(DialerPacingScheduler.GetDeadlineKey(_campaignQueue), fixture.BuildServices());

        // Assert: the holder may have measured the campaign before the change that asked for this run.
        fixture.DialerService.Verify(
            service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(2, deadlines.Scheduled.Count);
        Assert.Equal(clock.UtcNow.Add(_options.LockRetryDelay), deadlines.Scheduled.Last().DueUtc);
    }

    [Fact]
    public async Task TwoRunsOfOneCampaignAtOnce_OnlyOnePaces()
    {
        // Arrange: two nodes asked to pace the same campaign at the same moment.
        var clock = new TestClock();
        var fixture = new RunFixture();
        var schedulerA = CreateScheduler(new DueRecordingDeadlineScheduler(), clock);
        var schedulerB = CreateScheduler(new DueRecordingDeadlineScheduler(), clock);
        var inCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                inCycle.TrySetResult();
                await release.Task;

                return 0;
            });

        // Act
        var runA = schedulerA.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);
        await inCycle.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await schedulerB.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);
        release.SetResult();
        await runA;

        // Assert
        fixture.DialerService.Verify(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnOverDialingCampaign_IsLeftToThePredictivePacingScheduler()
    {
        // Arrange
        var fixture = new RunFixture();
        fixture.Profile.Mode = DialerMode.Predictive;
        fixture.Profile.PredictivePacingModel = PredictivePacingModel.OverDial;
        var scheduler = CreateScheduler(new DueRecordingDeadlineScheduler(), new TestClock());

        // Act
        await scheduler.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);

        // Assert
        fixture.DialerService.Verify(
            service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhileTheFeatureIsQuiescing_NothingIsPaced()
    {
        // Arrange
        var fixture = new RunFixture();
        fixture.WorkManager.Quiesce(ContactCenterConstants.Feature.DialerPaced);
        var scheduler = CreateScheduler(new DueRecordingDeadlineScheduler(), new TestClock());

        // Act
        await scheduler.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);

        // Assert
        fixture.DialerService.Verify(
            service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ARunThatLosesAWriteRace_PlacesNothing_AndTriesAgain()
    {
        // Arrange
        var clock = new TestClock();
        var deadlines = new DueRecordingDeadlineScheduler();
        var scheduler = CreateScheduler(deadlines, clock);
        var fixture = new RunFixture();
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        fixture.Session
            .Setup(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));

        // Act
        var next = await scheduler.RunAsync(fixture.BuildServices(), _campaignQueue, TestContext.Current.CancellationToken);

        // Assert: the rolled-back cycle's staged calls are discarded, and the campaign is paced again on what won.
        Assert.Null(next);
        fixture.Session.Verify(session => session.CancelAsync(), Times.Once);
        Assert.Equal(clock.UtcNow.Add(_options.LockRetryDelay), Assert.Single(deadlines.Scheduled).DueUtc);
    }

    [Fact]
    public async Task ABurstOfEvents_OnTheDeadlineScheduler_PacesOnceAfterTheDebounce_ThenAgainWhileCallsArePlaced()
    {
        // Arrange: the real in-process deadline scheduler on manual time, running each piece of work on the fixture's
        // services the way it would on a fresh shell scope.
        var clock = new TestClock();
        var time = new ManualTimeProvider(clock);
        var fixture = new RunFixture();
        using var deadlines = new ContactCenterDeadlineScheduler(clock, time, work => work(fixture.BuildServices()), NullLogger.Instance);
        var scheduler = CreateScheduler(deadlines, clock);
        var handler = new DialerPacingTriggerHandler(scheduler);
        var cycles = new List<DateTime>();
        var placed = new Queue<int>([1, 0]);
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cycles.Add(clock.UtcNow);

                return placed.Dequeue();
            });

        var start = clock.UtcNow;

        // Act: two agents come back and an unanswered attempt ends, all within a tenth of a second.
        await handler.HandleAsync(AgentAvailable("agent-1"), TestContext.Current.CancellationToken);
        await time.AdvanceAsync(TimeSpan.FromMilliseconds(50), deadlines.WhenIdleAsync);
        await handler.HandleAsync(AgentAvailable("agent-2"), TestContext.Current.CancellationToken);
        await time.AdvanceAsync(TimeSpan.FromMilliseconds(50), deadlines.WhenIdleAsync);
        var attemptEnded = new InteractionEvent { EventType = ContactCenterConstants.Events.DialerAttemptCompleted };
        attemptEnded.SetData(new CallLifecycleEventData { QueueId = _campaignQueue });
        await handler.HandleAsync(attemptEnded, TestContext.Current.CancellationToken);

        await time.AdvanceAsync(TimeSpan.FromMilliseconds(150), deadlines.WhenIdleAsync);
        var cyclesBeforeDebounce = cycles.Count;

        await time.AdvanceAsync(TimeSpan.FromSeconds(5), deadlines.WhenIdleAsync);

        // Assert: nothing before the debounce; then one cycle for the whole burst, measured from the first event, and a
        // second because the first placed a call; the second placed none, so pacing waits for the next event.
        Assert.Equal(0, cyclesBeforeDebounce);
        Assert.Equal(2, cycles.Count);
        Assert.InRange(cycles[0] - start, _options.Debounce, _options.Debounce + TimeSpan.FromMilliseconds(100));
        Assert.True(cycles[1] > cycles[0]);
        Assert.Equal(0, deadlines.Count);

        static InteractionEvent AgentAvailable(string agentId)
        {
            var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentStateChanged };
            interactionEvent.SetData(new AgentStateChangedEventData
            {
                AgentId = agentId,
                CurrentState = AgentPresenceStatus.Available,
                CampaignIds = ["campaign-1"],
            });

            return interactionEvent;
        }
    }

    [Fact]
    public async Task ARunDroppedBeforeItStarted_IsReArmedByTheBackstop_SoTheCampaignKeepsDialing()
    {
        // Arrange: the first time a run falls due its shell scope cannot be opened, so the deadline scheduler logs and drops
        // it before the run starts and clears the queue's pending mark.
        var clock = new TestClock();
        var time = new ManualTimeProvider(clock);
        var fixture = new RunFixture();
        var scopeFails = true;
        using var deadlines = new ContactCenterDeadlineScheduler(
            clock,
            time,
            work =>
            {
                if (scopeFails)
                {
                    scopeFails = false;

                    throw new InvalidOperationException("The shell scope could not be opened.");
                }

                return work(fixture.BuildServices());
            },
            NullLogger.Instance);
        var scheduler = CreateScheduler(deadlines, clock);
        var cycles = 0;
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .Callback(() => cycles++)
            .ReturnsAsync(0);

        // Act + Assert: the dropped run leaves its mark, and every event after it merges into a run that never comes.
        scheduler.Request(_campaignQueue);
        await time.AdvanceAsync(TimeSpan.FromSeconds(1), deadlines.WhenIdleAsync);
        Assert.Equal(0, cycles);

        scheduler.Request(_campaignQueue);
        Assert.Equal(0, deadlines.Count);
        await time.AdvanceAsync(TimeSpan.FromSeconds(30), deadlines.WhenIdleAsync);
        Assert.Equal(0, cycles);

        // The minute task's backstop does not merge into the mark: it arms the run again, and the campaign is paced.
        scheduler.RequestBackstop(_campaignQueue);
        await time.AdvanceAsync(TimeSpan.FromSeconds(1), deadlines.WhenIdleAsync);
        Assert.Equal(1, cycles);

        // And events pace it again from then on.
        scheduler.Request(_campaignQueue);
        await time.AdvanceAsync(TimeSpan.FromSeconds(1), deadlines.WhenIdleAsync);
        Assert.Equal(2, cycles);
    }

    [Fact]
    public async Task TheBackstop_WhileARequestIsPending_PacesTheCampaignOnce()
    {
        // Arrange
        var clock = new TestClock();
        var time = new ManualTimeProvider(clock);
        var fixture = new RunFixture();
        using var deadlines = new ContactCenterDeadlineScheduler(clock, time, work => work(fixture.BuildServices()), NullLogger.Instance);
        var scheduler = CreateScheduler(deadlines, clock);
        var cycles = 0;
        fixture.DialerService
            .Setup(service => service.RunCycleAsync(fixture.Profile, _campaignQueue, It.IsAny<CancellationToken>()))
            .Callback(() => cycles++)
            .ReturnsAsync(0);

        // Act: an agent frees up, and the minute task's backstop runs before the debounce is over.
        scheduler.Request(_campaignQueue);
        await time.AdvanceAsync(TimeSpan.FromMilliseconds(100), deadlines.WhenIdleAsync);
        scheduler.RequestBackstop(_campaignQueue);
        await time.AdvanceAsync(TimeSpan.FromSeconds(5), deadlines.WhenIdleAsync);

        // Assert: the backstop replaced the armed run rather than adding a second one.
        Assert.Equal(1, cycles);
        Assert.Equal(0, deadlines.Count);
    }

    [Fact]
    public void TheBackstop_WarnsWhenTheArmedRunNeverStarted_ButNotForARequestStillInItsDebounce()
    {
        // Arrange: a run armed on a deadline scheduler that never runs it.
        var clock = new TestClock();
        var logger = new RecordingLogger();
        var scheduler = CreateScheduler(new DueRecordingDeadlineScheduler(), clock, logger);

        // Act
        scheduler.Request(_campaignQueue);
        scheduler.RequestBackstop(_campaignQueue);
        var warningsWhileFresh = logger.Warnings.Count;

        clock.Advance(TimeSpan.FromMinutes(1));
        scheduler.RequestBackstop(_campaignQueue);

        // Assert
        Assert.Equal(0, warningsWhileFresh);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains(_campaignQueue, warning, StringComparison.Ordinal);
        Assert.Contains("never started", warning, StringComparison.Ordinal);
        Assert.Contains("00:01:00", warning, StringComparison.Ordinal);
    }

    private static DialerPacingScheduler CreateScheduler(IContactCenterDeadlineScheduler deadlines, TestClock clock, ILogger<DialerPacingScheduler> logger = null)
        => new(deadlines, clock, Options.Create(_options), logger ?? NullLogger<DialerPacingScheduler>.Instance);

    /// <summary>
    /// Keeps the formatted text of every warning.
    /// </summary>
    private sealed class RecordingLogger : ILogger<DialerPacingScheduler>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>
    /// The scoped services a run reads: a campaign whose waiting work was loaded under a Power profile.
    /// </summary>
    private sealed class RunFixture
    {
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

        public SingleHolderDistributedLock Lock { get; } = new();

        public TestContactCenterFeatureWorkManager WorkManager { get; } = new();

        public ServiceProvider BuildServices()
        {
            var queueItemStore = new Mock<IQueueItemStore>();
            queueItemStore
                .Setup(store => store.FindNextWaitingAsync(_campaignQueue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueItem { QueueId = _campaignQueue, ActivityItemId = "activity-1", DialerProfileId = Profile.ItemId });

            var profileReader = new Mock<IDialerProfileReader>();
            profileReader
                .Setup(reader => reader.FindByIdAsync(Profile.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Profile);

            var services = new ServiceCollection();
            services.AddSingleton(queueItemStore.Object);
            services.AddSingleton(profileReader.Object);
            services.AddSingleton(DialerService.Object);
            services.AddSingleton(Session.Object);
            services.AddSingleton<IDistributedLock>(Lock);
            services.AddSingleton<IContactCenterFeatureWorkManager>(WorkManager);

            return services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Records each deadline with when it falls due, and runs one when the test chooses.
    /// </summary>
    private sealed class DueRecordingDeadlineScheduler : IContactCenterDeadlineScheduler
    {
        private readonly ConcurrentDictionary<string, Func<IServiceProvider, CancellationToken, Task<DateTime?>>> _work = new(StringComparer.Ordinal);

        public List<(string Key, DateTime DueUtc)> Scheduled { get; } = [];

        public void Schedule(string key, DateTime dueUtc, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work)
        {
            Scheduled.Add((key, dueUtc));
            _work[key] = work;
        }

        public void Cancel(string key) => _work.TryRemove(key, out _);

        public async Task<DateTime?> RunAsync(string key, IServiceProvider services)
        {
            Assert.True(_work.TryRemove(key, out var work), $"Nothing was scheduled under '{key}'.");

            return await work(services, CancellationToken.None);
        }
    }

    /// <summary>
    /// A lock one holder at a time can take, as the Redis lock gives every node.
    /// </summary>
    private sealed class SingleHolderDistributedLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, byte> _held = new(StringComparer.Ordinal);

        public bool IsHeld(string key) => _held.ContainsKey(key);

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => _held.TryAdd(key, 0)
                ? Task.FromResult<ILocker>(new Locker(this, key))
                : throw new InvalidOperationException($"The lock '{key}' is already held.");

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>(_held.TryAdd(key, 0) ? (new Locker(this, key), true) : (null, false));

        public Task<bool> IsLockAcquiredAsync(string key) => Task.FromResult(IsHeld(key));

        private sealed class Locker : ILocker
        {
            private readonly SingleHolderDistributedLock _owner;
            private readonly string _key;

            public Locker(SingleHolderDistributedLock owner, string key)
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
